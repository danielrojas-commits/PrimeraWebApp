using Microsoft.AspNetCore.Mvc;
using PrimeraWebApp.Models;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

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
            using (MySqlConnection conexion = new MySqlConnection(_connectionString))
            {
                try
                {
                    conexion.Open();
                    ViewBag.MensajeConexion = "¡Conexion Web exitosa a MySQL usando appsettings.json!";
                    ViewBag.EstiloConexion = "success";
                }
                catch (MySqlException ex)
                {
                    ViewBag.MensajeConexion = $"Error al conectar a la base de datos: {ex.Message}";
                    ViewBag.EstiloConexion = "danger";
                }
            }

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
