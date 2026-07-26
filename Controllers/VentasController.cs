using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PrimeraWebApp.Models;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;
using System;

namespace PrimeraWebApp.Controllers
{
    public class VentasController : Controller
    {
        private readonly string _connectionString;

        public VentasController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarVenta(Venta nuevaVenta, List<Models.DetalleVenta> carrito)
        {
            if (nuevaVenta == null || carrito == null || !carrito.Any())
            {
                TempData["Error"] = "Datos de venta inválidos.";
                return RedirectToAction("Index");
            }

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var tx = conexion.BeginTransaction())
                {
                    try
                    {
                        // Insertar la cabecera de venta: intentar usar columnas comunes
                        var cols = new List<string>();
                        var vals = new List<string>();
                        var cmd = new MySqlCommand();
                        cmd.Connection = conexion;
                        cmd.Transaction = tx;

                        // fecha
                        cols.Add("`fecha`"); vals.Add("@fecha"); cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                        // total
                        cols.Add("`total`"); vals.Add("@total"); cmd.Parameters.AddWithValue("@total", Convert.ToDecimal(nuevaVenta.Total));
                        // estado
                        cols.Add("`estado`"); vals.Add("@estado"); cmd.Parameters.AddWithValue("@estado", nuevaVenta.Estado ? 1 : 0);

                        // id_trabajador si viene en el modelo
                        if (nuevaVenta.Id_trabajador.HasValue)
                        {
                            cols.Add("`id_trabajador`"); vals.Add("@id_trabajador"); cmd.Parameters.AddWithValue("@id_trabajador", nuevaVenta.Id_trabajador.Value);
                        }

                        cmd.CommandText = $"INSERT INTO ventas ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
                        cmd.ExecuteNonQuery();
                        var ventaId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", conexion, tx).ExecuteScalar());

                        // Calcular costo total de la venta consultando precio de costo en productos
                        int costoTotalVenta = 0;
                        foreach (var item in carrito)
                        {
                            // Obtener costo unitario: intentar columnas comunes
                            int costoUnitario = 0;
                            using (var pcmd = new MySqlCommand("SELECT * FROM productos WHERE id = @id LIMIT 1", conexion, tx))
                            {
                                pcmd.Parameters.AddWithValue("@id", item.Id_producto);
                                using (var r = pcmd.ExecuteReader())
                                {
                                    if (r.Read())
                                    {
                                        // buscar columna de costo o precio de compra
                                        try
                                        {
                                            for (int c = 0; c < r.FieldCount; c++)
                                            {
                                                var col = r.GetName(c).ToLowerInvariant();
                                                if ((col.Contains("costo") || col.Contains("precio_compra") || col.Contains("cost")) && !r.IsDBNull(c))
                                                {
                                                    int.TryParse(r.GetValue(c).ToString(), out costoUnitario);
                                                    break;
                                                }
                                            }

                                            // fallback: usar columna 'precio' si existe
                                            if (costoUnitario == 0)
                                            {
                                                var idx = -1;
                                                for (int c = 0; c < r.FieldCount; c++) if (string.Equals(r.GetName(c), "precio", StringComparison.OrdinalIgnoreCase)) { idx = c; break; }
                                                if (idx >= 0 && !r.IsDBNull(idx)) int.TryParse(r.GetValue(idx).ToString(), out costoUnitario);
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }

                            costoTotalVenta += costoUnitario * item.Cantidad;

                            // Intentar insertar detalle_ventas si la tabla existe
                            try
                            {
                                // detectar columnas simples: venta_id, producto_id, cantidad, precio_unitario
                                var detCols = new List<string> { "venta_id", "producto_id", "cantidad", "precio_unitario" };
                                var detParams = new List<string> { "@vId", "@prod", "@qty", "@punit" };
                                using (var idCmd = new MySqlCommand()) { idCmd.Connection = conexion; idCmd.Transaction = tx; idCmd.CommandText = $"INSERT INTO detalle_ventas (`venta_id`,`producto_id`,`cantidad`,`precio_unitario`) VALUES (@vId,@prod,@qty,@punit)"; idCmd.Parameters.AddWithValue("@vId", ventaId); idCmd.Parameters.AddWithValue("@prod", item.Id_producto); idCmd.Parameters.AddWithValue("@qty", item.Cantidad); idCmd.Parameters.AddWithValue("@punit", item.Precio_unitario); idCmd.ExecuteNonQuery(); }
                            }
                            catch { /* si no existe detalle_ventas, ignorar */ }
                        }

                        // Construir comando para insertar ganancia correctamente
                        var gcmd = new MySqlCommand(); gcmd.Connection = conexion; gcmd.Transaction = tx;
                        gcmd.CommandText = "INSERT INTO ganancias (`id_venta`,`fecha`,`total_venta`,`costo_total`,`ganancia_neta`) VALUES (@idv,@fecha,@totalv,@costot,@gnet)";
                        gcmd.Parameters.AddWithValue("@idv", ventaId);
                        gcmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                        gcmd.Parameters.AddWithValue("@totalv", Convert.ToInt32(nuevaVenta.Total));
                        gcmd.Parameters.AddWithValue("@costot", costoTotalVenta);
                        gcmd.Parameters.AddWithValue("@gnet", Convert.ToInt32(nuevaVenta.Total) - costoTotalVenta);
                        gcmd.ExecuteNonQuery();

                        tx.Commit();
                        TempData["Success"] = "Venta registrada y ganancia calculada.";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        TempData["Error"] = "Error al registrar la venta: " + ex.Message;
                        return RedirectToAction("Index");
                    }
                }
            }
        }

        public IActionResult Index()
        {
            var ventas = new List<Venta>();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();

                // Cargar todas las ventas
                using (var cmd = new MySqlCommand("SELECT * FROM ventas", conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var v = new Venta();

                        // Mapear columnas de forma tolerante
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i).ToLowerInvariant();
                            var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            try
                            {
                                switch (name)
                                {
                                    case "id_venta": v.Id_venta = Convert.ToInt32(val); break;
                                    case "fecha": v.Fecha = val != null ? Convert.ToDateTime(val) : DateTime.MinValue; break;
                                    case "total": v.Total = val != null ? Convert.ToDecimal(val) : 0m; break;
                                    case "estado": v.Estado = val != null && (Convert.ToInt32(val) == 1 || Convert.ToBoolean(val)); break;
                                    case "id_trabajador": v.Id_trabajador = val != null ? (int?)Convert.ToInt32(val) : null; break;
                                    default: break;
                                }
                            }
                            catch { /* ignorar conversiones que fallen */ }
                        }

                        ventas.Add(v);
                    }
                }

                // Si hay id_trabajador, cargar los trabajadores asociados en una sola consulta para evitar N+1
                var trabajadorIds = ventas.Where(x => x.Id_trabajador.HasValue).Select(x => x.Id_trabajador.Value).Distinct().ToList();
                if (trabajadorIds.Any())
                {
                    var paramNames = new List<string>();
                    for (int i = 0; i < trabajadorIds.Count; i++) paramNames.Add("@id" + i);

                    var sql = $"SELECT * FROM trabajadores WHERE id_trabajador IN ({string.Join(',', paramNames)})";
                    using (var cmd2 = new MySqlCommand(sql, conexion))
                    {
                        for (int i = 0; i < trabajadorIds.Count; i++) cmd2.Parameters.AddWithValue(paramNames[i], trabajadorIds[i]);

                        var trabajadores = new Dictionary<int, Trabajador>();
                        using (var r2 = cmd2.ExecuteReader())
                        {
                            while (r2.Read())
                            {
                                var t = new Trabajador();
                                for (int j = 0; j < r2.FieldCount; j++)
                                {
                                    var n = r2.GetName(j).ToLowerInvariant();
                                    var v = r2.IsDBNull(j) ? null : r2.GetValue(j);
                                    try
                                    {
                                        switch (n)
                                        {
                                            case "id_trabajador": t.Id_trabajador = Convert.ToInt32(v); break;
                                            case "nombre": t.Nombre = v?.ToString(); break;
                                            case "apellido": t.Apellido = v?.ToString(); break;
                                            case "correo": t.Correo = v?.ToString(); break;
                                            case "clave": t.Clave = v?.ToString(); break;
                                            case "admin": t.Admin = v != null && (Convert.ToInt32(v) == 1 || Convert.ToBoolean(v)); break;
                                            default: break;
                                        }
                                    }
                                    catch { }
                                }

                                trabajadores[t.Id_trabajador] = t;
                            }
                        }

                        // Asignar navegación
                        foreach (var venta in ventas)
                        {
                            if (venta.Id_trabajador.HasValue && trabajadores.ContainsKey(venta.Id_trabajador.Value))
                            {
                                venta.Trabajador = trabajadores[venta.Id_trabajador.Value];
                            }
                        }
                    }
                }
                // Calcular suma total de ganancias registradas (intentar varias columnas/tabla como fallback)
                decimal totalGanancias = 0m;

                try
                {
                    object res = null;

                    // 1) Intentar suma sobre la columna ganancia_neta en tabla ganancias
                    using (var sumCmd = new MySqlCommand("SELECT IFNULL(SUM(ganancia_neta),0) FROM ganancias", conexion))
                    {
                        res = sumCmd.ExecuteScalar();
                    }

                    // 2) Si el resultado es nulo o 0, intentar otras columnas comunes en tabla ganancias
                    if ((res == null || res == DBNull.Value || Convert.ToDecimal(res) == 0m))
                    {
                        using (var sumCmd = new MySqlCommand("SELECT IFNULL(SUM(total_venta),0) FROM ganancias", conexion))
                        {
                            res = sumCmd.ExecuteScalar();
                        }
                    }

                    // 3) Si sigue en 0, fallback: sumar la columna 'total' de la tabla ventas (ingresos brutos)
                    if (res == null || res == DBNull.Value || Convert.ToDecimal(res) == 0m)
                    {
                        using (var sumCmd = new MySqlCommand("SELECT IFNULL(SUM(total),0) FROM ventas", conexion))
                        {
                            res = sumCmd.ExecuteScalar();
                        }
                    }

                    // Convertir el resultado final a decimal de forma segura
                    if (res != null && res != DBNull.Value)
                    {
                        try { totalGanancias = Convert.ToDecimal(res); }
                        catch { decimal.TryParse(res.ToString(), out totalGanancias); }
                    }
                }
                catch
                {
                    totalGanancias = 0m;
                }

                ViewBag.TotalGanancias = totalGanancias;

            }

            return View(ventas);
        }
    }
}
