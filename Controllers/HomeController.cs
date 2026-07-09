using Microsoft.AspNetCore.Mvc;
using PrimeraWebApp.Models;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace PrimeraWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IActionResult Index()
        {
            var productos = new List<Dictionary<string, object>>();

            using (MySqlConnection conexion = new MySqlConnection(_connectionString))
            {
                try
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

                    ViewBag.MensajeConexion = "¡Conexión Web exitosa a MySQL usando appsettings.json!";
                    ViewBag.EstiloConexion = "success";
                }
                catch (MySqlException ex)
                {
                    ViewBag.MensajeConexion = $"Error al conectar a la base de datos: {ex.Message}";
                    ViewBag.EstiloConexion = "danger";
                }
            }

            // Intentar mapear id de categoría a nombre para mostrar el nombre en vez de la id
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

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
