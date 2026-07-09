using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrimeraWebApp.Controllers
{
    public class UsersController : Controller
    {
        private readonly string _connectionString;

        public UsersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public IActionResult Index()
        {
            var trabajadores = new List<Dictionary<string, object>>();
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM trabajadores", conexion))
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
                        trabajadores.Add(row);
                    }
                }
            }

            ViewBag.Workers = trabajadores;
            ViewBag.Columns = trabajadores.Any() ? trabajadores[0].Keys.ToList() : new List<string>();
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string nombre, string apellido, string correo)
        {
            if (string.IsNullOrWhiteSpace(nombre)) ModelState.AddModelError("nombre", "Nombre obligatorio");
            if (string.IsNullOrWhiteSpace(apellido)) ModelState.AddModelError("apellido", "Apellido obligatorio");
            if (string.IsNullOrWhiteSpace(correo)) ModelState.AddModelError("correo", "Correo obligatorio");

            if (!ModelState.IsValid) return View();

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("INSERT INTO trabajadores (nombre, apellido, correo) VALUES (@nombre, @apellido, @correo)", conexion))
                {
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    cmd.Parameters.AddWithValue("@apellido", apellido);
                    cmd.Parameters.AddWithValue("@correo", correo);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            Dictionary<string, object> trabajador = null;
            var keyCol = GetPrimaryKeyColumn("trabajadores") ?? "id";
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                var sql = $"SELECT * FROM trabajadores WHERE `{keyCol}`=@id LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            trabajador = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var name = reader.GetName(i);
                                trabajador[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                        }
                    }
                }
            }
            if (trabajador == null) return RedirectToAction("Index");
            ViewBag.Worker = trabajador;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, string correo)
        {
            // Sólo se permite editar el correo
            if (string.IsNullOrWhiteSpace(correo)) ModelState.AddModelError("correo", "Correo obligatorio");
            if (!ModelState.IsValid) return Edit(id);

            var keyCol = GetPrimaryKeyColumn("trabajadores") ?? "id";
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                var sql = $"UPDATE trabajadores SET correo=@correo WHERE `{keyCol}`=@id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@correo", correo);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var keyCol = GetPrimaryKeyColumn("trabajadores") ?? "id";
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                var sql = $"DELETE FROM trabajadores WHERE `{keyCol}`=@id";
                using (var cmd = new MySqlCommand(sql, conexion))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("Index");
        }

        private string GetPrimaryKeyColumn(string tableName)
        {
            try
            {
                using (var conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    using (var cmd = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table", conexion))
                    {
                        cmd.Parameters.AddWithValue("@table", tableName);
                        using (var reader = cmd.ExecuteReader())
                        {
                            var cols = new List<string>();
                            while (reader.Read()) cols.Add(reader.GetString(0));
                            if (!cols.Any()) return null;
                            return cols.FirstOrDefault(c => string.Equals(c, "id", StringComparison.OrdinalIgnoreCase))
                                   ?? cols.FirstOrDefault(c => c.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
                                   ?? cols.FirstOrDefault(c => c.EndsWith("Id"))
                                   ?? cols.First();
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
