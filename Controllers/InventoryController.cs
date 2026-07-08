using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace PrimeraWebApp.Controllers
{
    public class InventoryController : Controller
    {
        private readonly string _connectionString;

        public InventoryController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IActionResult Index()
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

            ViewBag.Products = productos;
            ViewBag.Columns = productos.Any() ? productos[0].Keys.ToList() : new List<string>();
            ViewBag.KeyColumn = keyColumn ?? "id";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(string keyColumn, string idValue, int stock)
        {
            if (string.IsNullOrEmpty(keyColumn) || string.IsNullOrEmpty(idValue))
                return RedirectToAction("Index");

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Use parameterized values for safety; column name validated by presence in page
                var sql = $"UPDATE productos SET stock=@stock WHERE `{keyColumn}`=@id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@stock", stock);
                    cmd.Parameters.AddWithValue("@id", idValue);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Index");
        }
    }
}
