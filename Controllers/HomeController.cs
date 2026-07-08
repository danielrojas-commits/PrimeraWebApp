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
