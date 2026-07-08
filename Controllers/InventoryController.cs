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

                // Si existe la columna 'estado', actualizar su valor según stock
                using (var check = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos' AND COLUMN_NAME='estado'", conexion))
                {
                    var exists = Convert.ToInt32(check.ExecuteScalar()) > 0;
                    if (exists)
                    {
                        var estado = stock == 0 ? "cero" : "disponible";
                        var sql2 = $"UPDATE productos SET estado=@estado WHERE `{keyColumn}`=@id";
                        using (var cmd2 = new MySqlCommand(sql2, conexion))
                        {
                            cmd2.Parameters.AddWithValue("@estado", estado);
                            cmd2.Parameters.AddWithValue("@id", idValue);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                }
            }

            return RedirectToAction("Index");
        }
    }
}
