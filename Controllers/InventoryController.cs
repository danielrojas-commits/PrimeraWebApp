using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace PrimeraWebApp.Controllers
{
    public class InventoryController : Controller
    {
        private readonly string _connectionString;

        public InventoryController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IActionResult Index(string q)
        {
            var productos = new List<Dictionary<string, object>>();

            using (var conexion = new MySqlConnection(_connectionString))
            {


                // Detectar columna de categoría en productos
                var possibleProdCatCols = new[] { "categoria_id", "categ_id", "categoriaId", "categoria", "categ" };
                string prodCatCol = null;
                using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos'", conexion))
                {
                    if (conexion.State != ConnectionState.Open) conexion.Open();
                    using (var reader = check.ExecuteReader())
                    {
                        var cols = new List<string>();
                        while (reader.Read()) cols.Add(reader.GetString(0));
                        prodCatCol = possibleProdCatCols.FirstOrDefault(c => cols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                    }
                }

                // Si existe columna de categoría y la tabla categ existe, realizar JOIN para obtener el nombre
                bool joined = false;
                if (!string.IsNullOrEmpty(prodCatCol))
                {
                    // Detectar columna clave de categ
                    string catKey = null;
                    using (var check2 = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='categ'", conexion))
                    {
                        if (conexion.State != ConnectionState.Open) conexion.Open();
                        using (var reader2 = check2.ExecuteReader())
                        {
                            var cols = new List<string>();
                            while (reader2.Read()) cols.Add(reader2.GetString(0));
                            if (cols.Any())
                            {
                                catKey = cols.FirstOrDefault(c => string.Equals(c, "id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? cols.FirstOrDefault(c => c.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? cols.FirstOrDefault(c => c.EndsWith("Id"))
                                         ?? cols.First();
                            }
                        }
                    }

                    // Detectar etiqueta de categ (nombre)
                    string catLabel = null;
                    using (var cmdLabel = new MySqlCommand("SELECT * FROM categ LIMIT 1", conexion))
                    {
                        if (conexion.State != ConnectionState.Open) conexion.Open();
                        using (var readerLabel = cmdLabel.ExecuteReader())
                        {
                            if (readerLabel.Read())
                            {
                                var firstCols = new List<string>();
                                for (int i = 0; i < readerLabel.FieldCount; i++) firstCols.Add(readerLabel.GetName(i));
                                catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => k != catKey)
                                           ?? catKey;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(catKey) && !string.IsNullOrEmpty(catLabel))
                    {
                        // Intentar ejecutar la consulta JOIN adaptada al proyecto (selección explícita de columnas)
                        try
                        {
                            // Selección usando JOIN dinámico para incluir el nombre de la categoría como 'nombre_categoria'
                            var sql = $"SELECT p.*, c.`{catLabel}` AS nombre_categoria FROM productos p INNER JOIN categ c ON p.`{prodCatCol}` = c.`{catKey}`";
                            // Si se proporcionó término de búsqueda, agregar cláusula WHERE sobre el nombre (case-insensitive)
                            if (!string.IsNullOrWhiteSpace(q))
                            {
                                sql += " WHERE LOWER(p.nombre) LIKE CONCAT('%', LOWER(@q), '%')";
                            }
                            using (var cmd = new MySqlCommand(sql, conexion))
                            {
                                if (!string.IsNullOrWhiteSpace(q)) cmd.Parameters.AddWithValue("@q", q.Trim());
                                if (conexion.State != ConnectionState.Open) conexion.Open();
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        var row = new Dictionary<string, object>();
                                        for (int i = 0; i < reader.FieldCount; i++)
                                        {
                                            var name = reader.GetName(i);
                                            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                        }
                                        productos.Add(row);
                                    }
                                }
                            }

                            joined = true;
                        }
                        catch (MySql.Data.MySqlClient.MySqlException)
                        {
                            // Si la consulta falla (nombres de columnas distintos), no usar JOIN aquí; el flujo continuará y hará SELECT * más abajo
                        }
                    }
                }

                if (!joined)
                {
                    // Consulta simple sin JOIN; si hay término de búsqueda, añadir cláusula WHERE
                    var sqlSimple = "SELECT * FROM productos";
                    if (!string.IsNullOrWhiteSpace(q)) sqlSimple += " WHERE LOWER(nombre) LIKE CONCAT('%', LOWER(@q), '%')";
                    using (var cmd = new MySqlCommand(sqlSimple, conexion))
                    {
                        if (!string.IsNullOrWhiteSpace(q)) cmd.Parameters.AddWithValue("@q", q.Trim());
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                productos.Add(row);
                            }
                        }
                    }
                }
            }

            // Intentar mapear id de categoría a nombre para mostrar el nombre en vez de la id
            if (productos.Any())
            {
                using (var conexion = new MySqlConnection(_connectionString))
                {
                conexion.Open();




                    // Detectar posible columna de categoría en productos
                    var possibleCols = new[] { "categoria_id", "categ_id", "categoriaId", "categoria", "categ" };
                    string prodCatCol = null;
                    using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos'", conexion))
                    using (var reader = check.ExecuteReader())
                    {
                        var cols = new List<string>();
                        while (reader.Read()) cols.Add(reader.GetString(0));
                        prodCatCol = possibleCols.FirstOrDefault(c => cols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                    }

                    if (!string.IsNullOrEmpty(prodCatCol))
                    {
                        // Cargar categorías y construir mapa key->label
                        var categorias = new List<Dictionary<string, object>>();
                        using (var cmd = new MySqlCommand("SELECT * FROM categ", conexion))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                categorias.Add(row);
                            }
                        }

                        if (categorias.Any())
                        {
                            var firstCols = categorias[0].Keys.ToList();
                            var catKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                                         ?? firstCols.First();

                            var catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => k != catKey)
                                           ?? catKey;

                            var map = new Dictionary<string, string>();
                            foreach (var c in categorias)
                            {
                                if (c.ContainsKey(catKey) && c[catKey] != null)
                                {
                                    var key = c[catKey].ToString();
                                    var label = c.ContainsKey(catLabel) && c[catLabel] != null ? c[catLabel].ToString() : key;
                                    if (!map.ContainsKey(key)) map[key] = label;
                                }
                            }

                            // Sustituir en los productos el id por el nombre cuando sea posible
                            foreach (var p in productos)
                            {
                                if (p.ContainsKey(prodCatCol) && p[prodCatCol] != null)
                                {
                                    var val = p[prodCatCol].ToString();
                                    if (map.ContainsKey(val))
                                    {
                                        p[prodCatCol] = map[val];
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Detect key column: prefer 'id', then '*_id', then first column
            string keyColumn = null;
            if (productos.Any())
            {
                var first = productos[0].Keys.ToList();
                keyColumn = first.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                            ?? first.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                            ?? first.FirstOrDefault(k => k.EndsWith("Id"))
                            ?? first.First();
            }

            // Si la consulta devolvió 'categoria_nombre' (por JOIN), sustituir la columna id de categoría
            if (productos.Any())
            {
                // detectar columna original de categoría en productos (si existe)
                var possibleProdCatCols = new[] { "categoria_id", "categ_id", "categoriaId", "categoria", "categ" };
                var prodCatKey = productos[0].Keys.FirstOrDefault(k => possibleProdCatCols.Any(p => string.Equals(p, k, System.StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrEmpty(prodCatKey) && productos[0].ContainsKey("nombre_categoria"))
                {
                    foreach (var p in productos)
                    {
                        if (p.ContainsKey("nombre_categoria") && p["nombre_categoria"] != null)
                        {
                            // reemplazar valor id por el nombre de categoría
                            p[prodCatKey] = p["nombre_categoria"];
                        }
                        // eliminar la columna auxiliar
                        if (p.ContainsKey("nombre_categoria")) p.Remove("nombre_categoria");
                    }
                }
            }

            // Mantener las columnas tal cual vienen de la consulta.
            // Si existe 'categoria_nom' en la fila, lo dejamos para que la vista muestre el nombre de categoría junto a la id_categoria.
            var columnsList = productos.Any() ? productos[0].Keys.ToList() : new List<string>();

            ViewBag.Products = productos;
            ViewBag.Columns = columnsList;
            ViewBag.KeyColumn = keyColumn ?? "id";

            return View();
        }

        [HttpGet]
        public IActionResult Sales()
        {
            var ventas = new List<Dictionary<string, object>>();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM ventas", conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        ventas.Add(row);
                    }
                }
            }

            // Si existe una columna de id de trabajador en la tabla 'ventas', reemplazar el id por el nombre completo
            try
            {
                var workerIdCandidates = new[] { "id_trabajador", "trabajador_id", "usuario_id", "user_id" };
                var ventasColsCheck = ventas.Any() ? ventas[0].Keys.ToList() : new List<string>();
                var workerCol = ventasColsCheck.FirstOrDefault(c => workerIdCandidates.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                if (!string.IsNullOrEmpty(workerCol) && ventas.Any())
                {
                    var ids = ventas.Where(v => v.ContainsKey(workerCol) && v[workerCol] != null)
                                    .Select(v => v[workerCol].ToString())
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .Distinct()
                                    .ToList();

                    var intIds = new List<int>();
                    foreach (var s in ids) if (int.TryParse(s, out var iv)) intIds.Add(iv);

                    if (intIds.Any())
                    {
                        // Cargar trabajadores en una sola consulta
                        var paramNames = new List<string>();
                        for (int i = 0; i < intIds.Count; i++) paramNames.Add("@id" + i);
                        var sql = $"SELECT id_trabajador, nombre, apellido FROM trabajadores WHERE id_trabajador IN ({string.Join(',', paramNames)})";
                        using (var conexion = new MySqlConnection(_connectionString))
                        {
                            conexion.Open();
                            using (var cmd = new MySqlCommand(sql, conexion))
                            {
                                for (int i = 0; i < intIds.Count; i++) cmd.Parameters.AddWithValue(paramNames[i], intIds[i]);
                                var map = new Dictionary<int, string>();
                                using (var r = cmd.ExecuteReader())
                                {
                                    while (r.Read())
                                    {
                                        try
                                        {
                                            var idv = r.IsDBNull(0) ? 0 : Convert.ToInt32(r.GetValue(0));
                                            var nombre = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                                            var apellido = r.FieldCount > 2 && !r.IsDBNull(2) ? r.GetString(2) : string.Empty;
                                            var full = (nombre ?? string.Empty) + (string.IsNullOrEmpty(apellido) ? string.Empty : " " + apellido);
                                            if (!map.ContainsKey(idv)) map[idv] = string.IsNullOrWhiteSpace(full) ? nombre : full;
                                        }
                                        catch { }
                                    }
                                }

                                // Reemplazar en las filas
                                foreach (var v in ventas)
                                {
                                    if (v.ContainsKey(workerCol) && v[workerCol] != null)
                                    {
                                        if (int.TryParse(v[workerCol].ToString(), out var idv) && map.ContainsKey(idv))
                                        {
                                            v[workerCol] = map[idv];
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* ignorar si no existe la tabla trabajadores o hay errores */ }

            // Si la tabla 'ventas' no contiene una columna de usuario, intentar obtenerla desde 'registros_ventas'
            var userColCandidates = new[] { "user_id", "usuario_id", "id_trabajador", "trabajador_id", "empleado_id", "created_by", "user", "usuario", "user_email", "usuario_email", "correo", "responsable", "registrado_por" };
            var ventasCols = ventas.Any() ? ventas[0].Keys.ToList() : new List<string>();
            var ventasHasUser = ventasCols.Any(c => userColCandidates.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

            if (!ventasHasUser)
            {
                // comprobar si existe tabla registros_ventas con referencia a venta
                try
                {
                    using (var conexion = new MySqlConnection(_connectionString))
                    {
                        conexion.Open();
                        using (var chk = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='registros_ventas'", conexion))
                        {
                            var exists = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                            if (exists)
                            {
                                // cargar registros_ventas
                                var reg = new List<Dictionary<string, object>>();
                                using (var rc = new MySqlCommand("SELECT * FROM registros_ventas", conexion))
                                using (var rr = rc.ExecuteReader())
                                {
                                    while (rr.Read())
                                    {
                                        var rrow = new Dictionary<string, object>();
                                        for (int i = 0; i < rr.FieldCount; i++) rrow[rr.GetName(i)] = rr.IsDBNull(i) ? null : rr.GetValue(i);
                                        reg.Add(rrow);
                                    }
                                }

                                if (reg.Any())
                                {
                                    // detectar columna que referencia a la venta y columna de usuario
                                    var regCols = reg[0].Keys.ToList();
                                    var rvVentaCol = regCols.FirstOrDefault(c => new[] { "venta_id", "ventaId", "id_venta", "ventas_id" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvUserCol = regCols.FirstOrDefault(c => userColCandidates.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                                    if (!string.IsNullOrEmpty(rvVentaCol) && !string.IsNullOrEmpty(rvUserCol))
                                    {
                                        // construir mapa ventaId -> user
                                        var map = new Dictionary<string, object>();
                                        foreach (var r in reg)
                                        {
                                            if (r.ContainsKey(rvVentaCol) && r[rvVentaCol] != null)
                                            {
                                                var key = r[rvVentaCol].ToString();
                                                if (!map.ContainsKey(key) && r.ContainsKey(rvUserCol)) map[key] = r[rvUserCol];
                                            }
                                        }

                                        // añadir columna 'usuario' a cada venta si existe mapping
                                        if (map.Any())
                                        {
                                            foreach (var v in ventas)
                                            {
                                                var idKey = ventasCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase)) ?? ventasCols.FirstOrDefault();
                                                if (idKey != null && v.ContainsKey(idKey) && v[idKey] != null)
                                                {
                                                    var vid = v[idKey].ToString();
                                                    if (map.ContainsKey(vid)) v[rvUserCol] = map[vid];
                                                }
                                            }

                                            // actualizar columnas para la vista
                                            ventasCols = ventas.Any() ? ventas[0].Keys.ToList() : ventasCols;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { /* ignorar errores de consulta y mostrar sin usuario */ }
            }

            ViewBag.Sales = ventas;
            ViewBag.Columns = ventas.Any() ? ventas[0].Keys.ToList() : ventasCols;
            return View();
        }

        [HttpGet]
        public IActionResult SaleReceipt(long id)
        {
            var header = new Dictionary<string, object>();
            var items = new List<Dictionary<string, object>>();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Detectar nombre de la columna clave en 'ventas' (por si no es 'id')
                var ventasCols = new List<string>();
                using (var vc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='ventas'", conexion))
                using (var vr = vc.ExecuteReader()) { while (vr.Read()) ventasCols.Add(vr.GetString(0)); }

                var headerKey = ventasCols.FirstOrDefault(c => new[] { "id", "venta_id", "id_venta", "ventas_id", "ventaId" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)))
                                ?? ventasCols.FirstOrDefault();

                if (string.IsNullOrEmpty(headerKey))
                {
                    throw new System.Exception("No se pudo detectar la columna clave de la tabla 'ventas'.");
                }

                // Cargar cabecera de ventas usando la columna detectada
                using (var cmd = new MySqlCommand($"SELECT * FROM ventas WHERE `{headerKey}`=@id LIMIT 1", conexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++) header[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                    }
                }

                // Cargar items: detectar columnas en detalle_ventas y llaves para unir con productos
                var detalleCols = new List<string>();
                using (var dc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='detalle_ventas'", conexion))
                using (var dr = dc.ExecuteReader()) { while (dr.Read()) detalleCols.Add(dr.GetString(0)); }

                var detalleProdCol = detalleCols.FirstOrDefault(c => new[] { "producto_id", "productoId", "prod_id", "id_producto", "producto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                var detalleHeaderRef = detalleCols.FirstOrDefault(c => new[] { "venta_id", "ventas_id", "id_venta", "id_venta" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                // Determinar clave primaria de productos para join
                var prodKeyCols = new List<string>();
                using (var pc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos'", conexion))
                using (var pr = pc.ExecuteReader()) { while (pr.Read()) prodKeyCols.Add(pr.GetString(0)); }
                var prodKey = prodKeyCols.FirstOrDefault(c => string.Equals(c, "id", System.StringComparison.OrdinalIgnoreCase)) ?? prodKeyCols.FirstOrDefault();

                // Construir consulta de items con JOIN si se detectaron columnas para relacionar
                if (!string.IsNullOrEmpty(detalleProdCol) && !string.IsNullOrEmpty(prodKey))
                {
                    var sql = $"SELECT dv.*, p.nombre FROM detalle_ventas dv LEFT JOIN productos p ON dv.`{detalleProdCol}` = p.`{prodKey}` WHERE ";
                    if (!string.IsNullOrEmpty(detalleHeaderRef)) sql += $"dv.`{detalleHeaderRef}`=@id";
                    else sql += "1=0"; // no hay columna de referencia -> no devolver items

                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                items.Add(row);
                            }
                        }
                    }
                }
            }

            ViewBag.VentaId = id;
            ViewBag.Header = header;
            ViewBag.Items = items;
            return View();
        }

        [HttpGet]
        public IActionResult RegisterSale()
        {
            var productos = new List<Dictionary<string, object>>();
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM productos", conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        productos.Add(row);
                    }
                }
            }

            // Mapear id de categoría a nombre (reutiliza lógica de Index)
            if (productos.Any())
            {
                using (var conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    var possibleCols = new[] { "categoria_id", "categ_id", "categoriaId", "categoria", "categ" };
                    string prodCatCol = null;
                    using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos'", conexion))
                    using (var reader = check.ExecuteReader())
                    {
                        var cols = new List<string>();
                        while (reader.Read()) cols.Add(reader.GetString(0));
                        prodCatCol = possibleCols.FirstOrDefault(c => cols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                    }

                    if (!string.IsNullOrEmpty(prodCatCol))
                    {
                        var categorias = new List<Dictionary<string, object>>();
                        using (var cmd = new MySqlCommand("SELECT * FROM categ", conexion))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                categorias.Add(row);
                            }
                        }

                        if (categorias.Any())
                        {
                            var firstCols = categorias[0].Keys.ToList();
                            var catKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                                         ?? firstCols.First();

                            var catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => k != catKey)
                                           ?? catKey;

                            var map = new Dictionary<string, string>();
                            foreach (var c in categorias)
                            {
                                if (c.ContainsKey(catKey) && c[catKey] != null)
                                {
                                    var key = c[catKey].ToString();
                                    var label = c.ContainsKey(catLabel) && c[catLabel] != null ? c[catLabel].ToString() : key;
                                    if (!map.ContainsKey(key)) map[key] = label;
                                }
                            }

                            foreach (var p in productos)
                            {
                                if (p.ContainsKey(prodCatCol) && p[prodCatCol] != null)
                                {
                                    var val = p[prodCatCol].ToString();
                                    if (map.ContainsKey(val)) p[prodCatCol] = map[val];
                                }
                            }
                        }
                    }
                }
            }

            ViewBag.Products = productos;
            ViewBag.Columns = productos.Any() ? productos[0].Keys.ToList() : new List<string>();
            // key column detection
            ViewBag.KeyColumn = productos.Any() ? (productos[0].Keys.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                ?? productos[0].Keys.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                ?? productos[0].Keys.FirstOrDefault(k => k.EndsWith("Id"))
                ?? productos[0].Keys.First()) : "id";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterSale(string keyColumn, string idValue, int cantidad)
        {
            if (string.IsNullOrEmpty(keyColumn) || string.IsNullOrEmpty(idValue) || cantidad <= 0)
            {
                TempData["Error"] = "Parámetros inválidos para registrar la venta.";
                return RedirectToAction("RegisterSale");
            }

            // Información del usuario actual (disponible para usar en las inserciones)
            string currentUserEmail = Request.Cookies.ContainsKey("user_email") ? Request.Cookies["user_email"] : null;
            long? currentUserId = null;

            // Preferir cookie user_id si está presente (evita consulta adicional)
            try
            {
                if (Request.Cookies.ContainsKey("user_id") && long.TryParse(Request.Cookies["user_id"], out var parsedId))
                {
                    currentUserId = parsedId;
                }
            }
            catch { /* ignorar errores de parseo */ }

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Si no tenemos user_id en cookie, intentar resolver id del trabajador asociado al correo
                try
                {
                    if (!currentUserId.HasValue && !string.IsNullOrEmpty(currentUserEmail))
                    {
                        var workerCols = new List<string>();
                        using (var wc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='trabajadores'", conexion))
                        using (var wr = wc.ExecuteReader()) { while (wr.Read()) workerCols.Add(wr.GetString(0)); }

                        var workerKey = workerCols.FirstOrDefault(c => string.Equals(c, "id", System.StringComparison.OrdinalIgnoreCase))
                                        ?? workerCols.FirstOrDefault(c => c.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                                        ?? workerCols.FirstOrDefault(c => c.EndsWith("Id"))
                                        ?? workerCols.FirstOrDefault();

                        if (!string.IsNullOrEmpty(workerKey))
                        {
                            using (var uc = new MySqlCommand($"SELECT `{workerKey}` FROM trabajadores WHERE correo=@correo LIMIT 1", conexion))
                            {
                                uc.Parameters.AddWithValue("@correo", currentUserEmail);
                                var v = uc.ExecuteScalar();
                                if (v != null && v != System.DBNull.Value)
                                {
                                    if (long.TryParse(v.ToString(), out var idv)) currentUserId = idv;
                                }
                            }
                        }
                    }
                }
                catch { /* ignorar si no hay tabla trabajadores o la cookie no apunta a un usuario válido */ }
                // Obtener stock y precio actuales
                decimal precio = 0;
                int stock = 0;
                using (var cmd = new MySqlCommand($"SELECT stock, precio FROM productos WHERE `{keyColumn}`=@id LIMIT 1", conexion))
                {
                    cmd.Parameters.AddWithValue("@id", idValue);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (!reader.IsDBNull(0)) int.TryParse(reader.GetValue(0).ToString(), out stock);
                            if (reader.FieldCount > 1 && !reader.IsDBNull(1)) decimal.TryParse(reader.GetValue(1).ToString(), out precio);
                        }
                    }
                }

                if (cantidad > stock)
                {
                    TempData["Error"] = "Cantidad superior al stock disponible.";
                    return RedirectToAction("RegisterSale");
                }

                // Buscar tablas relacionadas a ventas
                var tableNames = new List<string>();
                using (var tcmd = new MySqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE()", conexion))
                using (var tr = tcmd.ExecuteReader())
                {
                    while (tr.Read()) tableNames.Add(tr.GetString(0));
                }

                string headerTable = null;
                string itemsTable = null;
                string ingresosTable = null;

                // Si las tablas exactas existen, priorizarlas (caso provisto por el usuario)
                if (tableNames.Any(t => string.Equals(t, "ventas", System.StringComparison.OrdinalIgnoreCase))
                    && tableNames.Any(t => string.Equals(t, "detalle_ventas", System.StringComparison.OrdinalIgnoreCase)))
                {
                    headerTable = tableNames.First(t => string.Equals(t, "ventas", System.StringComparison.OrdinalIgnoreCase));
                    itemsTable = tableNames.First(t => string.Equals(t, "detalle_ventas", System.StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    var headerCandidates = new[] { "ventas", "venta", "sales", "sale", "ventas_cab", "ventas_header" };
                    var itemsCandidates = new[] { "venta_items", "venta_detalle", "ventas_items", "sale_items", "sales_items", "detalle_venta", "detalle_ventas" };
                    var ingresosCandidates = new[] { "ingresos", "ingreso", "earnings", "receipts" };

                    headerTable = headerCandidates.FirstOrDefault(h => tableNames.Any(t => string.Equals(t, h, System.StringComparison.OrdinalIgnoreCase)));
                    itemsTable = itemsCandidates.FirstOrDefault(h => tableNames.Any(t => string.Equals(t, h, System.StringComparison.OrdinalIgnoreCase)));
                    ingresosTable = ingresosCandidates.FirstOrDefault(h => tableNames.Any(t => string.Equals(t, h, System.StringComparison.OrdinalIgnoreCase)));
                }

                // Usar transacción si trabajamos con tablas de ventas
                if (!string.IsNullOrEmpty(headerTable) && !string.IsNullOrEmpty(itemsTable))
                {
                    using (var tx = conexion.BeginTransaction())
                    {
                        try
                        {
                            // Si las tablas son exactamente 'ventas' y 'detalle_ventas', usar flujo fijo y profesional
                            if (string.Equals(headerTable, "ventas", System.StringComparison.OrdinalIgnoreCase) && string.Equals(itemsTable, "detalle_ventas", System.StringComparison.OrdinalIgnoreCase))
                            {
                                // Detectar columnas en 'ventas' y 'detalle_ventas' para usar nombres reales
                                var ventasCols = new List<string>();
                                using (var vc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='ventas'", conexion, tx))
                                using (var vr = vc.ExecuteReader()) { while (vr.Read()) ventasCols.Add(vr.GetString(0)); }

                                var detalleCols = new List<string>();
                                using (var dc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='detalle_ventas'", conexion, tx))
                                using (var dr = dc.ExecuteReader()) { while (dr.Read()) detalleCols.Add(dr.GetString(0)); }

                                var fechaCol = ventasCols.FirstOrDefault(c => new[] { "fecha", "created_at", "timestamp", "fecha_registro", "fecha_creacion", "created" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var totalVentaCol = ventasCols.FirstOrDefault(c => new[] { "total", "importe", "monto", "total_amount", "amount" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                                // Preparar insert cabecera con las columnas detectadas
                                var headerCols = new List<string>();
                                var headerParams = new List<string>();
                                var headerCmd = new MySqlCommand(); headerCmd.Connection = conexion; headerCmd.Transaction = tx;
                                if (!string.IsNullOrEmpty(fechaCol)) { headerCols.Add($"`{fechaCol}`"); headerParams.Add("@fecha"); headerCmd.Parameters.AddWithValue("@fecha", System.DateTime.Now); }
                                if (!string.IsNullOrEmpty(totalVentaCol)) { headerCols.Add($"`{totalVentaCol}`"); headerParams.Add("@total"); headerCmd.Parameters.AddWithValue("@total", cantidad * precio); }

                                // Intentar añadir información del usuario que realiza la venta si la tabla 'ventas' tiene una columna adecuada
                                var userColCandidates = new[] { "user_id", "usuario_id", "id_trabajador", "trabajador_id", "empleado_id", "created_by", "user", "usuario", "user_email", "usuario_email", "correo", "responsable", "registrado_por" };
                                var ventasUserCol = ventasCols.FirstOrDefault(c => userColCandidates.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                if (!string.IsNullOrEmpty(ventasUserCol))
                                {
                                    headerCols.Add($"`{ventasUserCol}`");
                                    headerParams.Add("@userVal");
                                    if (currentUserId.HasValue)
                                        headerCmd.Parameters.AddWithValue("@userVal", currentUserId.Value);
                                    else
                                        headerCmd.Parameters.AddWithValue("@userVal", (object)(currentUserEmail ?? string.Empty));
                                }

                                if (!headerCols.Any())
                                {
                                    // No hay columnas esperadas en ventas: fallback a flujo genérico
                                    throw new System.Exception("La tabla 'ventas' no contiene columnas esperadas (fecha/total).");
                                }

                                headerCmd.CommandText = $"INSERT INTO ventas ({string.Join(", ", headerCols)}) VALUES ({string.Join(", ", headerParams)})";
                                headerCmd.ExecuteNonQuery();
                                long ventaId = Convert.ToInt64(new MySqlCommand("SELECT LAST_INSERT_ID()", conexion, tx).ExecuteScalar());

                                // Detectar nombres de columnas en detalle_ventas
                                var prodCol = detalleCols.FirstOrDefault(c => new[] { "producto_id", "productoId", "prod_id", "id_producto", "producto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var qtyCol = detalleCols.FirstOrDefault(c => new[] { "cantidad", "qty", "quantity", "cantidad_vendida" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var unitPriceCol = detalleCols.FirstOrDefault(c => new[] { "precio_unitario", "precio", "unit_price", "price" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var totalItemCol = detalleCols.FirstOrDefault(c => new[] { "total", "importe", "monto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var headerRefCol = detalleCols.FirstOrDefault(c => new[] { "venta_id", "ventas_id", "id_venta" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                                var itemCols = new List<string>();
                                var itemParams = new List<string>();
                                var itemCmd = new MySqlCommand(); itemCmd.Connection = conexion; itemCmd.Transaction = tx;
                                if (!string.IsNullOrEmpty(headerRefCol)) { itemCols.Add($"`{headerRefCol}`"); itemParams.Add("@ventaId"); itemCmd.Parameters.AddWithValue("@ventaId", ventaId); }
                                if (!string.IsNullOrEmpty(prodCol)) { itemCols.Add($"`{prodCol}`"); itemParams.Add("@prodId"); itemCmd.Parameters.AddWithValue("@prodId", idValue); }
                                if (!string.IsNullOrEmpty(qtyCol)) { itemCols.Add($"`{qtyCol}`"); itemParams.Add("@qty"); itemCmd.Parameters.AddWithValue("@qty", cantidad); }
                                if (!string.IsNullOrEmpty(unitPriceCol)) { itemCols.Add($"`{unitPriceCol}`"); itemParams.Add("@unitPrice"); itemCmd.Parameters.AddWithValue("@unitPrice", precio); }
                                if (!string.IsNullOrEmpty(totalItemCol)) { itemCols.Add($"`{totalItemCol}`"); itemParams.Add("@totalI"); itemCmd.Parameters.AddWithValue("@totalI", cantidad * precio); }

                                if (!itemCols.Any())
                                {
                                    throw new System.Exception("La tabla 'detalle_ventas' no contiene columnas esperadas (producto_id/cantidad/precio_unitario/total).");
                                }

                                itemCmd.CommandText = $"INSERT INTO detalle_ventas ({string.Join(", ", itemCols)}) VALUES ({string.Join(", ", itemParams)})";
                                itemCmd.ExecuteNonQuery();

                                // Actualizar stock dentro de la transacción
                                using (var upd = new MySqlCommand($"UPDATE productos SET stock=@stock WHERE `{keyColumn}`=@id", conexion, tx))
                                {
                                    upd.Parameters.AddWithValue("@stock", stock - cantidad);
                                    upd.Parameters.AddWithValue("@id", idValue);
                                    var affected = upd.ExecuteNonQuery();
                                    if (affected == 0) throw new System.Exception("No se pudo actualizar el stock del producto.");
                                }

                                // Intentar registrar ingreso si existe tabla ingresos
                                if (tableNames.Any(t => string.Equals(t, "ingresos", System.StringComparison.OrdinalIgnoreCase) || string.Equals(t, "ingreso", System.StringComparison.OrdinalIgnoreCase)))
                                {
                                    var ingresosTableName = tableNames.First(t => string.Equals(t, "ingresos", System.StringComparison.OrdinalIgnoreCase) || string.Equals(t, "ingreso", System.StringComparison.OrdinalIgnoreCase));
                                    var ingresosCols = new List<string>();
                                    using (var icmd = new MySqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{ingresosTableName}'", conexion, tx))
                                    using (var ir2 = icmd.ExecuteReader()) { while (ir2.Read()) ingresosCols.Add(ir2.GetString(0)); }

                                    var ingAmountCol = ingresosCols.FirstOrDefault(c => new[] { "importe", "monto", "total", "amount", "ingreso" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var ingDateCol = ingresosCols.FirstOrDefault(c => new[] { "fecha", "created_at", "timestamp", "fecha_registro" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                                    if (!string.IsNullOrEmpty(ingAmountCol))
                                    {
                                        var ingCols = new List<string>();
                                        var ingParams = new List<string>();
                                        var ingCmd = new MySqlCommand(); ingCmd.Connection = conexion; ingCmd.Transaction = tx;
                                        ingCols.Add($"`{ingAmountCol}`"); ingParams.Add("@ingTotal"); ingCmd.Parameters.AddWithValue("@ingTotal", cantidad * precio);
                                        if (!string.IsNullOrEmpty(ingDateCol)) { ingCols.Add($"`{ingDateCol}`"); ingParams.Add("@ingFecha"); ingCmd.Parameters.AddWithValue("@ingFecha", System.DateTime.Now); }
                                        ingCmd.CommandText = $"INSERT INTO {ingresosTableName} ({string.Join(", ", ingCols)}) VALUES ({string.Join(", ", ingParams)})";
                                        ingCmd.ExecuteNonQuery();
                                    }
                                }

                                // Registrar también en 'registros_ventas' si existe (donde se centralizan ventas)
                                if (tableNames.Any(t => string.Equals(t, "registros_ventas", System.StringComparison.OrdinalIgnoreCase)))
                                {
                                    var regVentasCols = new List<string>();
                                    using (var rvc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='registros_ventas'", conexion, tx))
                                    using (var rvr = rvc.ExecuteReader()) { while (rvr.Read()) regVentasCols.Add(rvr.GetString(0)); }

                                    var rvVentaCol = regVentasCols.FirstOrDefault(c => new[] { "venta_id", "ventaId", "id_venta" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvProdCol = regVentasCols.FirstOrDefault(c => new[] { "producto_id", "productoId", "prod_id", "id_producto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvQtyCol = regVentasCols.FirstOrDefault(c => new[] { "cantidad", "qty", "quantity" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvTotalCol = regVentasCols.FirstOrDefault(c => new[] { "total", "importe", "monto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvDateCol = regVentasCols.FirstOrDefault(c => new[] { "fecha", "created_at", "timestamp" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvActionCol = regVentasCols.FirstOrDefault(c => new[] { "accion", "action", "tipo" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    var rvDetailCol = regVentasCols.FirstOrDefault(c => new[] { "detalle", "descripcion", "info" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                                    var colsRV = new List<string>();
                                    var paramsRV = new List<string>();
                                    var cmdRV = new MySqlCommand(); cmdRV.Connection = conexion; cmdRV.Transaction = tx;
                                    if (!string.IsNullOrEmpty(rvVentaCol)) { colsRV.Add($"`{rvVentaCol}`"); paramsRV.Add("@rvVentaId"); cmdRV.Parameters.AddWithValue("@rvVentaId", ventaId); }
                                    if (!string.IsNullOrEmpty(rvProdCol)) { colsRV.Add($"`{rvProdCol}`"); paramsRV.Add("@rvProdId"); cmdRV.Parameters.AddWithValue("@rvProdId", idValue); }
                                    if (!string.IsNullOrEmpty(rvQtyCol)) { colsRV.Add($"`{rvQtyCol}`"); paramsRV.Add("@rvQty"); cmdRV.Parameters.AddWithValue("@rvQty", cantidad); }
                                    if (!string.IsNullOrEmpty(rvTotalCol)) { colsRV.Add($"`{rvTotalCol}`"); paramsRV.Add("@rvTotal"); cmdRV.Parameters.AddWithValue("@rvTotal", cantidad * precio); }
                                    if (!string.IsNullOrEmpty(rvDateCol)) { colsRV.Add($"`{rvDateCol}`"); paramsRV.Add("@rvFecha"); cmdRV.Parameters.AddWithValue("@rvFecha", System.DateTime.Now); }
                                    if (!string.IsNullOrEmpty(rvActionCol)) { colsRV.Add($"`{rvActionCol}`"); paramsRV.Add("@rvAccion"); cmdRV.Parameters.AddWithValue("@rvAccion", "venta"); }
                                    if (!string.IsNullOrEmpty(rvDetailCol)) { colsRV.Add($"`{rvDetailCol}`"); paramsRV.Add("@rvDetalle"); cmdRV.Parameters.AddWithValue("@rvDetalle", $"venta {ventaId}: {cantidad} x {precio}"); }

                                    // Añadir columna de usuario en registros_ventas si existe
                                    var userColCandidatesRv = new[] { "user_id", "usuario_id", "id_trabajador", "trabajador_id", "empleado_id", "created_by", "user", "usuario", "user_email", "usuario_email", "correo", "responsable", "registrado_por" };
                                    var rvUserCol = regVentasCols.FirstOrDefault(c => userColCandidatesRv.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                    if (!string.IsNullOrEmpty(rvUserCol))
                                    {
                                        colsRV.Add($"`{rvUserCol}`");
                                        paramsRV.Add("@rvUser");
                                        if (currentUserId.HasValue) cmdRV.Parameters.AddWithValue("@rvUser", currentUserId.Value);
                                        else cmdRV.Parameters.AddWithValue("@rvUser", (object)(currentUserEmail ?? string.Empty));
                                    }

                                    if (colsRV.Any())
                                    {
                                        cmdRV.CommandText = $"INSERT INTO registros_ventas ({string.Join(", ", colsRV)}) VALUES ({string.Join(", ", paramsRV)})";
                                        cmdRV.ExecuteNonQuery();
                                    }
                                }

                                tx.Commit();
                                TempData["Success"] = "Venta registrada correctamente (ventas / detalle_ventas).";
                                return RedirectToAction("SaleReceipt", new { id = ventaId });
                            }
                            else
                            {
                                // flujo genérico anterior cuando no son las tablas exactas
                                // Detectar columnas posibles en la cabecera
                                var headerCols = new List<string>();
                                using (var hc = new MySqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{headerTable}'", conexion, tx))
                                using (var hr = hc.ExecuteReader())
                                {
                                    while (hr.Read()) headerCols.Add(hr.GetString(0));
                                }

                                var dateCol = headerCols.FirstOrDefault(c => new[] { "fecha", "created_at", "timestamp", "fecha_registro", "fecha_creacion", "created" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var totalCol = headerCols.FirstOrDefault(c => new[] { "total", "importe", "monto", "total_amount", "amount" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var idColIsAuto = true; // asumimos id autoincremental

                                var headerInsertCols = new List<string>();
                                var headerInsertParams = new List<string>();
                                var headerCmd = new MySqlCommand();
                                headerCmd.Connection = conexion;
                                headerCmd.Transaction = tx;

                                if (!string.IsNullOrEmpty(dateCol)) { headerInsertCols.Add($"`{dateCol}`"); headerInsertParams.Add("@fecha"); headerCmd.Parameters.AddWithValue("@fecha", System.DateTime.Now); }
                                if (!string.IsNullOrEmpty(totalCol)) { headerInsertCols.Add($"`{totalCol}`"); headerInsertParams.Add("@total"); headerCmd.Parameters.AddWithValue("@total", cantidad * precio); }

                                // Añadir columna de usuario en la cabecera genérica si existe
                                var userColCandidatesHeader = new[] { "user_id", "usuario_id", "id_trabajador", "trabajador_id", "empleado_id", "created_by", "user", "usuario", "user_email", "usuario_email", "correo", "responsable", "registrado_por" };
                                var headerUserCol = headerCols.FirstOrDefault(c => userColCandidatesHeader.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                if (!string.IsNullOrEmpty(headerUserCol))
                                {
                                    headerInsertCols.Add($"`{headerUserCol}`");
                                    headerInsertParams.Add("@userVal");
                                    if (currentUserId.HasValue) headerCmd.Parameters.AddWithValue("@userVal", currentUserId.Value);
                                    else headerCmd.Parameters.AddWithValue("@userVal", (object)(currentUserEmail ?? string.Empty));
                                }

                                string headerSql = headerInsertCols.Any() ? $"INSERT INTO {headerTable} ({string.Join(", ", headerInsertCols)}) VALUES ({string.Join(", ", headerInsertParams)})" : null;
                                long insertedId = -1;
                                if (!string.IsNullOrEmpty(headerSql))
                                {
                                    headerCmd.CommandText = headerSql;
                                    headerCmd.ExecuteNonQuery();
                                    using (var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conexion, tx))
                                    {
                                        insertedId = Convert.ToInt64(idCmd.ExecuteScalar());
                                    }
                                }

                                // Insertar línea en items (genérico)
                                var itemCols = new List<string>();
                                using (var ic = new MySqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{itemsTable}'", conexion, tx))
                                using (var ir = ic.ExecuteReader())
                                {
                                    while (ir.Read()) itemCols.Add(ir.GetString(0));
                                }

                                var prodIdCol = itemCols.FirstOrDefault(c => new[] { "producto_id", "productoId", "prod_id", "id_producto", "producto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var qtyCol = itemCols.FirstOrDefault(c => new[] { "cantidad", "qty", "quantity", "cantidad_vendida" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var unitPriceCol = itemCols.FirstOrDefault(c => new[] { "precio_unitario", "precio", "unit_price", "price" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var totalItemCol = itemCols.FirstOrDefault(c => new[] { "total", "importe", "monto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var headerRefCol = itemCols.FirstOrDefault(c => itemCols.Any() && headerCols.Any() && (string.Equals(c, "venta_id", System.StringComparison.OrdinalIgnoreCase) || string.Equals(c, "ventas_id", System.StringComparison.OrdinalIgnoreCase) || string.Equals(c, headerCols.FirstOrDefault() + "_id", System.StringComparison.OrdinalIgnoreCase)));

                                var itemInsertCols = new List<string>();
                                var itemInsertParams = new List<string>();
                                var itemCmd = new MySqlCommand();
                                itemCmd.Connection = conexion;
                                itemCmd.Transaction = tx;

                                if (!string.IsNullOrEmpty(headerRefCol) && insertedId > 0) { itemInsertCols.Add($"`{headerRefCol}`"); itemInsertParams.Add("@headerId"); itemCmd.Parameters.AddWithValue("@headerId", insertedId); }
                                if (!string.IsNullOrEmpty(prodIdCol)) { itemInsertCols.Add($"`{prodIdCol}`"); itemInsertParams.Add("@prodId"); itemCmd.Parameters.AddWithValue("@prodId", idValue); }
                                if (!string.IsNullOrEmpty(qtyCol)) { itemInsertCols.Add($"`{qtyCol}`"); itemInsertParams.Add("@qty"); itemCmd.Parameters.AddWithValue("@qty", cantidad); }
                                if (!string.IsNullOrEmpty(unitPriceCol)) { itemInsertCols.Add($"`{unitPriceCol}`"); itemInsertParams.Add("@unitPrice"); itemCmd.Parameters.AddWithValue("@unitPrice", precio); }
                                if (!string.IsNullOrEmpty(totalItemCol)) { itemInsertCols.Add($"`{totalItemCol}`"); itemInsertParams.Add("@totalI"); itemCmd.Parameters.AddWithValue("@totalI", cantidad * precio); }

                                if (itemInsertCols.Any())
                                {
                                    var itemSql = $"INSERT INTO {itemsTable} ({string.Join(", ", itemInsertCols)}) VALUES ({string.Join(", ", itemInsertParams)})";
                                    itemCmd.CommandText = itemSql;
                                    itemCmd.ExecuteNonQuery();
                                }

                                // Actualizar stock (dentro de la transacción)
                                using (var upd = new MySqlCommand($"UPDATE productos SET stock=@stock WHERE `{keyColumn}`=@id", conexion, tx))
                                {
                                    upd.Parameters.AddWithValue("@stock", stock - cantidad);
                                    upd.Parameters.AddWithValue("@id", idValue);
                                    upd.ExecuteNonQuery();
                                }

                                tx.Commit();
                                TempData["Success"] = "Venta registrada correctamente (tablas de ventas detectadas).";
                                return RedirectToAction("RegisterSale");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            tx.Rollback();
                            TempData["Error"] = "Error al registrar la venta en tablas de ventas: " + ex.Message;
                            return RedirectToAction("RegisterSale");
                        }
                    }
                }
                else
                {
                    // Fallback: comportamiento previo (registros simple)
                    // Actualizar stock
                    var newStock = stock - cantidad;
                    using (var upd = new MySqlCommand($"UPDATE productos SET stock=@stock WHERE `{keyColumn}`=@id", conexion))
                    {
                        upd.Parameters.AddWithValue("@stock", newStock);
                        upd.Parameters.AddWithValue("@id", idValue);
                        upd.ExecuteNonQuery();
                    }

                    // Registrar en registros (como antes)
                    var registrosCols = new List<string>();
                    using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='registros'", conexion))
                    using (var reader = check.ExecuteReader()) { while (reader.Read()) registrosCols.Add(reader.GetString(0)); }

                    if (registrosCols.Any())
                    {
                        var prodColCandidates = new[] { "producto_id", "productoId", "prod_id", "id_producto", "producto", "productoid", "productoid" };
                        var dateColCandidates = new[] { "fecha", "created_at", "timestamp", "fecha_registro", "fecha_creacion", "created" };
                        var qtyColCandidates = new[] { "cantidad", "qty", "quantity", "cantidad_vendida", "cantidad_venta" };
                        var amountColCandidates = new[] { "importe", "monto", "total", "ingreso", "ingresos", "precio_total" };
                        var actionColCandidates = new[] { "accion", "action", "tipo", "tipo_accion" };
                        var detailColCandidates = new[] { "detalle", "descripcion", "info" };

                        string prodCol = prodColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                        string dateCol = dateColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                        string qtyCol = qtyColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                        string amountCol = amountColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                        string actionCol = actionColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                        string detailCol = detailColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));

                        var insertCols = new List<string>();
                        var insertParams = new List<string>();
                        var cmdIns = new MySqlCommand();
                        cmdIns.Connection = conexion;

                        if (!string.IsNullOrEmpty(prodCol)) { insertCols.Add($"`{prodCol}`"); insertParams.Add("@prodId"); cmdIns.Parameters.AddWithValue("@prodId", idValue); }
                        if (!string.IsNullOrEmpty(dateCol)) { insertCols.Add($"`{dateCol}`"); insertParams.Add("@fecha"); cmdIns.Parameters.AddWithValue("@fecha", System.DateTime.Now); }
                        if (!string.IsNullOrEmpty(qtyCol)) { insertCols.Add($"`{qtyCol}`"); insertParams.Add("@cantidad"); cmdIns.Parameters.AddWithValue("@cantidad", cantidad); }
                        if (!string.IsNullOrEmpty(amountCol)) { insertCols.Add($"`{amountCol}`"); insertParams.Add("@total"); cmdIns.Parameters.AddWithValue("@total", cantidad * precio); }
                        if (!string.IsNullOrEmpty(actionCol)) { insertCols.Add($"`{actionCol}`"); insertParams.Add("@accion"); cmdIns.Parameters.AddWithValue("@accion", "venta"); }
                        if (!string.IsNullOrEmpty(detailCol)) { insertCols.Add($"`{detailCol}`"); insertParams.Add("@detalle"); var detalle = $"venta: {cantidad} x {precio} = {cantidad * precio}"; cmdIns.Parameters.AddWithValue("@detalle", detalle); }

                        // Añadir información del usuario en 'registros' si la tabla tiene una columna adecuada
                        var userColCandidatesReg = new[] { "user_id", "usuario_id", "id_trabajador", "trabajador_id", "empleado_id", "created_by", "user", "usuario", "user_email", "usuario_email", "correo", "responsable", "registrado_por" };
                        var regUserCol = registrosCols.FirstOrDefault(c => userColCandidatesReg.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                        if (!string.IsNullOrEmpty(regUserCol))
                        {
                            insertCols.Add($"`{regUserCol}`");
                            insertParams.Add("@userVal");
                            if (currentUserId.HasValue) cmdIns.Parameters.AddWithValue("@userVal", currentUserId.Value);
                            else cmdIns.Parameters.AddWithValue("@userVal", (object)(currentUserEmail ?? string.Empty));
                        }

                        if (insertCols.Any()) { var insertSql = $"INSERT INTO registros ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertParams)})"; cmdIns.CommandText = insertSql; cmdIns.ExecuteNonQuery(); }
                        // Además insertar en 'registros_ventas' si existe (registro centralizado de ventas)
                        using (var chkRv = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='registros_ventas'", conexion))
                        {
                            var existsRv = Convert.ToInt32(chkRv.ExecuteScalar()) > 0;
                            if (existsRv)
                            {
                                var regVentasCols = new List<string>();
                                using (var rvc = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='registros_ventas'", conexion))
                                using (var rvr = rvc.ExecuteReader()) { while (rvr.Read()) regVentasCols.Add(rvr.GetString(0)); }

                                var rvVentaCol = regVentasCols.FirstOrDefault(c => new[] { "venta_id", "ventaId", "id_venta" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var rvProdCol = regVentasCols.FirstOrDefault(c => new[] { "producto_id", "productoId", "prod_id", "id_producto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var rvQtyCol = regVentasCols.FirstOrDefault(c => new[] { "cantidad", "qty", "quantity" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var rvTotalCol = regVentasCols.FirstOrDefault(c => new[] { "total", "importe", "monto" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var rvDateCol = regVentasCols.FirstOrDefault(c => new[] { "fecha", "created_at", "timestamp" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var rvActionCol = regVentasCols.FirstOrDefault(c => new[] { "accion", "action", "tipo" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                var rvDetailCol = regVentasCols.FirstOrDefault(c => new[] { "detalle", "descripcion", "info" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                                var colsRV = new List<string>();
                                var paramsRV = new List<string>();
                                var cmdRV = new MySqlCommand(); cmdRV.Connection = conexion;
                                if (!string.IsNullOrEmpty(rvVentaCol)) { colsRV.Add($"`{rvVentaCol}`"); paramsRV.Add("@rvVentaId"); cmdRV.Parameters.AddWithValue("@rvVentaId", DBNull.Value); }
                                if (!string.IsNullOrEmpty(rvProdCol)) { colsRV.Add($"`{rvProdCol}`"); paramsRV.Add("@rvProdId"); cmdRV.Parameters.AddWithValue("@rvProdId", idValue); }
                                if (!string.IsNullOrEmpty(rvQtyCol)) { colsRV.Add($"`{rvQtyCol}`"); paramsRV.Add("@rvQty"); cmdRV.Parameters.AddWithValue("@rvQty", cantidad); }
                                if (!string.IsNullOrEmpty(rvTotalCol)) { colsRV.Add($"`{rvTotalCol}`"); paramsRV.Add("@rvTotal"); cmdRV.Parameters.AddWithValue("@rvTotal", cantidad * precio); }
                                if (!string.IsNullOrEmpty(rvDateCol)) { colsRV.Add($"`{rvDateCol}`"); paramsRV.Add("@rvFecha"); cmdRV.Parameters.AddWithValue("@rvFecha", System.DateTime.Now); }
                                if (!string.IsNullOrEmpty(rvActionCol)) { colsRV.Add($"`{rvActionCol}`"); paramsRV.Add("@rvAccion"); cmdRV.Parameters.AddWithValue("@rvAccion", "venta"); }
                                if (!string.IsNullOrEmpty(rvDetailCol)) { colsRV.Add($"`{rvDetailCol}`"); paramsRV.Add("@rvDetalle"); cmdRV.Parameters.AddWithValue("@rvDetalle", $"venta: {cantidad} x {precio}"); }

                                // Añadir columna de usuario en registros_ventas si existe
                                var userColCandidatesRv = new[] { "user_id", "usuario_id", "id_trabajador", "trabajador_id", "empleado_id", "created_by", "user", "usuario", "user_email", "usuario_email", "correo", "responsable", "registrado_por" };
                                var rvUserCol = regVentasCols.FirstOrDefault(c => userColCandidatesRv.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                                if (!string.IsNullOrEmpty(rvUserCol))
                                {
                                    colsRV.Add($"`{rvUserCol}`");
                                    paramsRV.Add("@rvUser");
                                    if (currentUserId.HasValue) cmdRV.Parameters.AddWithValue("@rvUser", currentUserId.Value);
                                    else cmdRV.Parameters.AddWithValue("@rvUser", (object)(currentUserEmail ?? string.Empty));
                                }

                                if (colsRV.Any()) { cmdRV.CommandText = $"INSERT INTO registros_ventas ({string.Join(", ", colsRV)}) VALUES ({string.Join(", ", paramsRV)})"; cmdRV.ExecuteNonQuery(); }
                            }
                        }
                    }

                    TempData["Success"] = "Venta registrada correctamente.";
                    return RedirectToAction("RegisterSale");
                }
            }
        }

        [HttpGet]
        public IActionResult Historial()
        {
            var registros = new List<Dictionary<string, object>>();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM registros", conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        registros.Add(row);
                    }
                }
            }
            // Si la tabla 'registros' contiene una columna que referencia al producto (id), reemplazar por nombre del producto
            if (registros.Any())
            {
                // Detectar posible columna de producto en registros
                var registrosCols = registros[0].Keys.ToList();
                var prodColCandidates = new[] { "producto_id", "productoId", "prod_id", "id_producto", "producto", "productoid", "producto_id" };
                string prodColInReg = prodColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrEmpty(prodColInReg))
                {
                    // Cargar productos para construir mapa id->nombre
                    var productos = new List<Dictionary<string, object>>();
                    using (var conexion = new MySqlConnection(_connectionString))
                    {
                        conexion.Open();
                        using (var cmd = new MySqlCommand("SELECT * FROM productos", conexion))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }
                                productos.Add(row);
                            }
                        }
                    }

                    if (productos.Any())
                    {
                        var firstCols = productos[0].Keys.ToList();
                        var prodKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                                      ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                                      ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                                      ?? firstCols.First();

                        var prodLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                                       ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                                       ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                                       ?? firstCols.FirstOrDefault(k => k != prodKey)
                                       ?? prodKey;

                        var map = new Dictionary<string, string>();
                        foreach (var p in productos)
                        {
                            if (p.ContainsKey(prodKey) && p[prodKey] != null)
                            {
                                var key = p[prodKey].ToString();
                                var label = p.ContainsKey(prodLabel) && p[prodLabel] != null ? p[prodLabel].ToString() : key;
                                if (!map.ContainsKey(key)) map[key] = label;
                            }
                        }

                        // Reemplazar en registros el id por el nombre cuando sea posible
                        foreach (var r in registros)
                        {
                            if (r.ContainsKey(prodColInReg) && r[prodColInReg] != null)
                            {
                                var val = r[prodColInReg].ToString();
                                if (map.ContainsKey(val))
                                {
                                    r[prodColInReg] = map[val];
                                }
                            }
                        }
                    }
                }
            }

            string keyColumn = null;
            if (registros.Any())
            {
                var first = registros[0].Keys.ToList();
                keyColumn = first.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                            ?? first.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                            ?? first.FirstOrDefault(k => k.EndsWith("Id"))
                            ?? first.First();
            }

            ViewBag.Records = registros;
            ViewBag.Columns = registros.Any() ? registros[0].Keys.ToList() : new List<string>();
            ViewBag.KeyColumn = keyColumn ?? "id";

            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            var categorias = new List<Dictionary<string, object>>();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM categ", conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        categorias.Add(row);
                    }
                }
            }

            // Determinar columna clave y etiqueta para categorías
            string catKey = null;
            string catLabel = null;
            if (categorias.Any())
            {
                var firstCols = categorias[0].Keys.ToList();
                catKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                         ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                         ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                         ?? firstCols.First();

                catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                           ?? firstCols.FirstOrDefault(k => k != catKey)
                           ?? catKey;
            }

            ViewBag.Categories = categorias;
            ViewBag.CatKeyColumn = catKey ?? "id";
            ViewBag.CatLabelColumn = catLabel ?? "nombre";

            return View();
        }

        // Vistas y acciones para administrar categorías: Crear, Editar, Eliminar
        [HttpGet]
        public IActionResult CreateCategory()
        {
            var categorias = new List<Dictionary<string, object>>();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM categ", conexion))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        categorias.Add(row);
                    }
                }
            }

            string catKey = null;
            string catLabel = null;
            if (categorias.Any())
            {
                var firstCols = categorias[0].Keys.ToList();
                catKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                         ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                         ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                         ?? firstCols.First();

                catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                           ?? firstCols.FirstOrDefault(k => k != catKey)
                           ?? catKey;
            }

            ViewBag.Categories = categorias;
            ViewBag.CatKeyColumn = catKey ?? "id";
            ViewBag.CatLabelColumn = catLabel ?? "nombre";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCategory(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                ModelState.AddModelError("nombre", "El nombre de la categoría es obligatorio.");
                return View();
            }

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("INSERT INTO categ (nombre) VALUES (@nombre)", conexion))
                {
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Create");
        }

        [HttpGet]
        public IActionResult EditCategory(int id)
        {
            Dictionary<string, object> categoria = null;
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM categ WHERE id=@id LIMIT 1", conexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            categoria = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var name = reader.GetName(i);
                                categoria[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                        }
                    }
                }
            }

            if (categoria == null) return RedirectToAction("Create");
            ViewBag.Categoria = categoria;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditCategory(int id, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                ModelState.AddModelError("nombre", "El nombre de la categoría es obligatorio.");
            }
            if (!ModelState.IsValid)
            {
                return EditCategory(id);
            }

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("UPDATE categ SET nombre=@nombre WHERE id=@id", conexion))
                {
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Create");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id)
        {
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("DELETE FROM categ WHERE id=@id", conexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("Create");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string nombre, decimal precio, int stock, int? categoriaId)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                ModelState.AddModelError("nombre", "El nombre del producto es obligatorio.");
            }
            if (precio < 0)
            {
                ModelState.AddModelError("precio", "El precio no puede ser negativo.");
            }
            if (stock < 0)
            {
                ModelState.AddModelError("stock", "El stock no puede ser negativo.");
            }

            if (!categoriaId.HasValue)
            {
                ModelState.AddModelError("categoriaId", "Debe seleccionar una categoría.");
            }

            if (!ModelState.IsValid)
            {
                // Recargar categorías para mostrar la vista con errores
                var categorias = new List<Dictionary<string, object>>();

                using (var conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    using (var cmd = new MySqlCommand("SELECT * FROM categ", conexion))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var name = reader.GetName(i);
                                row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            categorias.Add(row);
                        }
                    }
                }

                string catKey = null;
                string catLabel = null;
                if (categorias.Any())
                {
                    var firstCols = categorias[0].Keys.ToList();
                    catKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                             ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                             ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                             ?? firstCols.First();

                    catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                               ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                               ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                               ?? firstCols.FirstOrDefault(k => k != catKey)
                               ?? catKey;
                }

                ViewBag.Categories = categorias;
                ViewBag.CatKeyColumn = catKey ?? "id";
                ViewBag.CatLabelColumn = catLabel ?? "nombre";

                return View();
            }

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Determinar si la tabla productos tiene una columna para la categoría
                var possibleCols = new[] { "categoria_id", "categ_id", "categoriaId", "categoria", "categ" };
                string prodCatCol = null;
                using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos'", conexion))
                using (var reader = check.ExecuteReader())
                {
                    var cols = new List<string>();
                    while (reader.Read()) cols.Add(reader.GetString(0));
                    prodCatCol = possibleCols.FirstOrDefault(c => cols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                }

                if (prodCatCol == null)
                {
                    // Si no existe columna para categoría, devolver error para que el usuario lo vea
                    ModelState.AddModelError("categoriaId", "La tabla 'productos' no contiene una columna para almacenar la categoría. Añádela o contacte al administrador.");
                    return View();
                }
                // Detectar si la tabla productos tiene columna para almacenar el nombre de la categoría (categoria_nom)
                bool hasCategoriaNom = false;
                using (var checkCatNom = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos' AND COLUMN_NAME='categoria_nom'", conexion))
                {
                    hasCategoriaNom = Convert.ToInt32(checkCatNom.ExecuteScalar()) > 0;
                }

                // Detectar si la tabla productos tiene columna 'estado'
                bool hasEstado = false;
                using (var checkEstado = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos' AND COLUMN_NAME='estado'", conexion))
                {
                    hasEstado = Convert.ToInt32(checkEstado.ExecuteScalar()) > 0;
                }

                // Si existe la columna categoria_nom y se proporcionó categoriaId, obtener el nombre desde la tabla categ
                string categoriaNomValue = null;
                if (hasCategoriaNom && categoriaId.HasValue)
                {
                    using (var cmdGetCat = new MySqlCommand($"SELECT nombre FROM categ WHERE id=@id LIMIT 1", conexion))
                    {
                        cmdGetCat.Parameters.AddWithValue("@id", categoriaId.Value);
                        var scalar = cmdGetCat.ExecuteScalar();
                        if (scalar != null && scalar != System.DBNull.Value)
                        {
                            categoriaNomValue = scalar.ToString();
                        }
                    }
                }

                // Construir INSERT adaptado: incluir columna id de categoría, opcionalmente categoria_nom y estado
                string query;
                var insertCols = new List<string> { "nombre", "stock", "precio" };
                var insertVals = new List<string> { "@nombre", "@stock", "@precio" };

                if (!string.IsNullOrEmpty(prodCatCol))
                {
                    insertCols.Add($"`{prodCatCol}`");
                    insertVals.Add("@categoria");
                }

                if (hasCategoriaNom)
                {
                    insertCols.Add("`categoria_nom`");
                    insertVals.Add("@categoria_nom");
                }

                if (hasEstado)
                {
                    insertCols.Add("`estado`");
                    insertVals.Add("@estado");
                }

                query = $"INSERT INTO productos ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)})";

                using (var cmd = new MySqlCommand(query, conexion))
                {
                    cmd.Parameters.AddWithValue("@nombre", (nombre ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@stock", stock);
                    cmd.Parameters.AddWithValue("@precio", precio);
                    if (!string.IsNullOrEmpty(prodCatCol))
                    {
                        cmd.Parameters.AddWithValue("@categoria", categoriaId.Value);
                    }
                    if (hasCategoriaNom)
                    {
                        cmd.Parameters.AddWithValue("@categoria_nom", categoriaNomValue ?? string.Empty);
                    }
                    if (hasEstado)
                    {
                        // Usar 1 para true en BD (compatibilidad con tinyint)
                        cmd.Parameters.AddWithValue("@estado", 1);
                    }
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (MySql.Data.MySqlClient.MySqlException ex)
                    {
                        // MySQL duplicate entry error
                        if (ex.Number == 1062)
                        {
                            ModelState.AddModelError("nombre", "este producto ya existe");

                            // Recargar categorías para mostrar la vista con el error
                            var categorias = new List<Dictionary<string, object>>();
                            using (var cmd2 = new MySqlCommand("SELECT * FROM categ", conexion))
                            using (var reader = cmd2.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var row = new Dictionary<string, object>();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        var name = reader.GetName(i);
                                        row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    }
                                    categorias.Add(row);
                                }
                            }

                            string catKey = null;
                            string catLabel = null;
                            if (categorias.Any())
                            {
                                var firstCols = categorias[0].Keys.ToList();
                                catKey = firstCols.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? firstCols.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                                         ?? firstCols.FirstOrDefault(k => k.EndsWith("Id"))
                                         ?? firstCols.First();

                                catLabel = firstCols.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => string.Equals(k, "descripcion", System.StringComparison.OrdinalIgnoreCase))
                                           ?? firstCols.FirstOrDefault(k => k != catKey)
                                           ?? catKey;
                            }

                            ViewBag.Categories = categorias;
                            ViewBag.CatKeyColumn = catKey ?? "id";
                            ViewBag.CatLabelColumn = catLabel ?? "nombre";

                            return View();
                        }
                        throw;
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string keyColumn, string idValue)
        {
            if (string.IsNullOrEmpty(keyColumn) || string.IsNullOrEmpty(idValue))
                return RedirectToAction("Index");

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Usar consulta parametrizada para borrar el registro
                var sql = $"DELETE FROM productos WHERE `{keyColumn}`=@id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@id", idValue);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(string keyColumn, string idValue, int stock, decimal? precio)
        {
            if (string.IsNullOrEmpty(keyColumn) || string.IsNullOrEmpty(idValue))
                return RedirectToAction("Index");

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Obtener valor de stock y precio anteriores para registrar historial
                int oldStockVal = -1;
                decimal oldPriceVal = -1;

                // Comprobar si la columna 'precio' existe en la tabla productos
                bool hasPrecioCol = false;
                using (var checkPrecio = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos' AND COLUMN_NAME='precio'", conexion))
                {
                    hasPrecioCol = Convert.ToInt32(checkPrecio.ExecuteScalar()) > 0;
                }

                // Construir SELECT acorde a la existencia de la columna precio
                var selectSql = hasPrecioCol ? $"SELECT stock, precio FROM productos WHERE `{keyColumn}`=@id" : $"SELECT stock FROM productos WHERE `{keyColumn}`=@id";
                using (var getCmd = new MySqlCommand(selectSql, conexion))
                {
                    getCmd.Parameters.AddWithValue("@id", idValue);
                    using (var reader = getCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (!reader.IsDBNull(0)) int.TryParse(reader.GetValue(0).ToString(), out oldStockVal);
                            if (hasPrecioCol && reader.FieldCount > 1 && !reader.IsDBNull(1)) decimal.TryParse(reader.GetValue(1).ToString(), out oldPriceVal);
                        }
                    }
                }

                // Use parameterized values for safety; column name validated by presence in page
                // Construir UPDATE dinámico para stock y opcionalmente precio
                var setParts = new List<string> { "stock=@stock" };
                if (precio.HasValue)
                {
                    if (hasPrecioCol)
                    {
                        setParts.Add("precio=@precio");
                    }
                    else
                    {
                        // La tabla no tiene columna 'precio' — no actualizamos precio
                        TempData["Error"] = "La tabla 'productos' no contiene la columna 'precio'; el valor no fue actualizado.";
                    }
                }

                var sql = $"UPDATE productos SET {string.Join(", ", setParts)} WHERE `{keyColumn}`=@id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@stock", stock);
                    if (precio.HasValue) cmd.Parameters.AddWithValue("@precio", precio.Value);
                    cmd.Parameters.AddWithValue("@id", idValue);
                    cmd.ExecuteNonQuery();
                }

                // Si existe la columna 'estado', actualizar su valor según stock
                using (var check = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos' AND COLUMN_NAME='estado'", conexion))
                {
                    var exists = Convert.ToInt32(check.ExecuteScalar()) > 0;
                    if (exists)
                    {
                        // Usar valores numéricos: 0 cuando stock==0, 1 cuando hay stock
                        var estadoVal = stock == 0 ? 0 : 1;
                        var sql2 = $"UPDATE productos SET estado=@estado WHERE `{keyColumn}`=@id";
                        using (var cmd2 = new MySqlCommand(sql2, conexion))
                        {
                            cmd2.Parameters.AddWithValue("@estado", estadoVal);
                            cmd2.Parameters.AddWithValue("@id", idValue);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                }

                // Registrar cambio en la tabla 'registros' si existe: insertar id del producto y marca temporal exacta
                var registrosCols = new List<string>();
                using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='registros'", conexion))
                using (var reader = check.ExecuteReader())
                {
                    while (reader.Read()) registrosCols.Add(reader.GetString(0));
                }

                if (registrosCols.Any())
                {
                    var prodColCandidates = new[] { "producto_id", "productoId", "prod_id", "id_producto", "producto", "productoid", "productoid" };
                    var dateColCandidates = new[] { "fecha", "created_at", "timestamp", "fecha_registro", "fecha_creacion", "created" };

                    string prodCol = prodColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                    string dateCol = dateColCandidates.FirstOrDefault(c => registrosCols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));

                    // Detectar columnas para antiguo/nuevo stock y precio, acción y detalle (variantes comunes)
                    string oldStockCol = registrosCols.FirstOrDefault(c => new[] { "old_stock", "stock_old", "previous_stock", "stock_prev", "stock_before", "oldStock", "stockOld" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                    string newStockCol = registrosCols.FirstOrDefault(c => new[] { "new_stock", "stock_new", "current_stock", "stock_current", "stock_after", "newStock", "stockNew" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                    string oldPriceCol = registrosCols.FirstOrDefault(c => new[] { "old_precio", "precio_old", "old_price", "price_old", "precio_prev", "precio_before", "oldPrecio", "oldPrice" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                    string newPriceCol = registrosCols.FirstOrDefault(c => new[] { "new_precio", "precio_new", "new_price", "price_new", "precio_after", "newPrecio", "newPrice" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                    string actionCol = registrosCols.FirstOrDefault(c => new[] { "accion", "action", "tipo", "tipo_accion" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));
                    string detailCol = registrosCols.FirstOrDefault(c => new[] { "detalle", "descripcion", "descripcion_cambio", "detalle_cambio", "info" }.Any(p => string.Equals(p, c, System.StringComparison.OrdinalIgnoreCase)));

                    // Construir lista de columnas y parámetros para insertar los cambios detectados
                    var insertCols = new List<string>();
                    var insertParams = new List<string>();
                    var cmdIns = new MySqlCommand();
                    cmdIns.Connection = conexion;

                    if (!string.IsNullOrEmpty(prodCol))
                    {
                        insertCols.Add($"`{prodCol}`");
                        insertParams.Add("@prodId");
                        cmdIns.Parameters.AddWithValue("@prodId", idValue);
                    }

                    if (!string.IsNullOrEmpty(dateCol))
                    {
                        insertCols.Add($"`{dateCol}`");
                        insertParams.Add("@fecha");
                        cmdIns.Parameters.AddWithValue("@fecha", System.DateTime.Now);
                    }

                    if (!string.IsNullOrEmpty(oldStockCol))
                    {
                        insertCols.Add($"`{oldStockCol}`");
                        insertParams.Add("@oldStock");
                        cmdIns.Parameters.AddWithValue("@oldStock", oldStockVal);
                    }
                    if (!string.IsNullOrEmpty(newStockCol))
                    {
                        insertCols.Add($"`{newStockCol}`");
                        insertParams.Add("@newStock");
                        cmdIns.Parameters.AddWithValue("@newStock", stock);
                    }

                    if (!string.IsNullOrEmpty(oldPriceCol))
                    {
                        insertCols.Add($"`{oldPriceCol}`");
                        insertParams.Add("@oldPrice");
                        cmdIns.Parameters.AddWithValue("@oldPrice", oldPriceVal);
                    }
                    if (!string.IsNullOrEmpty(newPriceCol))
                    {
                        insertCols.Add($"`{newPriceCol}`");
                        insertParams.Add("@newPrice");
                        cmdIns.Parameters.AddWithValue("@newPrice", precio.HasValue ? precio.Value : (object)System.DBNull.Value);
                    }

                    if (!string.IsNullOrEmpty(actionCol))
                    {
                        insertCols.Add($"`{actionCol}`");
                        insertParams.Add("@accion");

                        // Determinar tipo de acción según cambios realizados
                        bool stockChanged = oldStockVal != stock;
                        bool priceChanged = precio.HasValue && hasPrecioCol && oldPriceVal != precio.Value;

                        string accionValor;
                        if (stockChanged && !priceChanged) accionValor = "actualizacion de stock";
                        else if (!stockChanged && priceChanged) accionValor = "reajuste de precios";
                        else if (stockChanged && priceChanged) accionValor = "actualizacion de stock y reajuste de precios";
                        else accionValor = "sin cambios";

                        cmdIns.Parameters.AddWithValue("@accion", accionValor);
                    }

                    if (!string.IsNullOrEmpty(detailCol))
                    {
                        insertCols.Add($"`{detailCol}`");
                        insertParams.Add("@detalle");
                        var detalle = $"stock: {oldStockVal} -> {stock}" + (precio.HasValue ? $", precio: {oldPriceVal} -> {precio.Value}" : "");
                        cmdIns.Parameters.AddWithValue("@detalle", detalle);
                    }

                    // Si no se detectaron columnas adicionales, caer al comportamiento original: insertar prodId y/o fecha cuando existan
                    if (!insertCols.Any())
                    {
                        // mantener comportamiento previo
                        if (!string.IsNullOrEmpty(prodCol) && !string.IsNullOrEmpty(dateCol))
                        {
                            using (var cmd2 = new MySqlCommand($"INSERT INTO registros (`{prodCol}`, `{dateCol}`) VALUES (@prodId, @fecha)", conexion))
                            {
                                cmd2.Parameters.AddWithValue("@prodId", idValue);
                                cmd2.Parameters.AddWithValue("@fecha", System.DateTime.Now);
                                cmd2.ExecuteNonQuery();
                            }
                        }
                        else if (!string.IsNullOrEmpty(prodCol))
                        {
                            using (var cmd2 = new MySqlCommand($"INSERT INTO registros (`{prodCol}`) VALUES (@prodId)", conexion))
                            {
                                cmd2.Parameters.AddWithValue("@prodId", idValue);
                                cmd2.ExecuteNonQuery();
                            }
                        }
                        else if (!string.IsNullOrEmpty(dateCol))
                        {
                            using (var cmd2 = new MySqlCommand($"INSERT INTO registros (`{dateCol}`) VALUES (@fecha)", conexion))
                            {
                                cmd2.Parameters.AddWithValue("@fecha", System.DateTime.Now);
                                cmd2.ExecuteNonQuery();
                            }
                        }
                    }
                    else
                    {
                        var insertSql = $"INSERT INTO registros ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertParams)})";
                        cmdIns.CommandText = insertSql;
                        cmdIns.ExecuteNonQuery();
                    }
                }
            }

            return RedirectToAction("Index");
        }
    }
}
