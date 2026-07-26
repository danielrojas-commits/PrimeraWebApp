using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using PrimeraWebApp.Models;
// Requiere el paquete BCrypt.Net-Next
using BCrypt.Net;

namespace PrimeraWebApp.Controllers
{
    public class UsersController : Controller
    {
        private readonly string _connectionString;

        public UsersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // Comprueba si el usuario actual (por cookie user_email) tiene admin=1
        private bool CurrentUserIsAdmin()
        {
            try
            {
                if (!Request.Cookies.ContainsKey("user_email")) return false;
                var correo = Request.Cookies["user_email"];
                if (string.IsNullOrWhiteSpace(correo)) return false;

                using (var conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();

                    // Verificar que la columna 'admin' existe
                    using (var chk = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='trabajadores' AND COLUMN_NAME='admin'", conexion))
                    {
                        var has = chk.ExecuteScalar();
                        if (has == null || Convert.ToInt32(has) == 0) return false;
                    }

                    using (var cmd = new MySqlCommand("SELECT admin FROM trabajadores WHERE correo=@correo LIMIT 1", conexion))
                    {
                        cmd.Parameters.AddWithValue("@correo", correo);
                        var val = cmd.ExecuteScalar();
                        if (val == null || val == DBNull.Value) return false;
                        if (val is bool b) return b;
                        if (int.TryParse(val.ToString(), out var iv)) return iv == 1;
                        if (bool.TryParse(val.ToString(), out var bv)) return bv;
                        return string.Equals(val.ToString(), "1") || string.Equals(val.ToString(), "true", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                return false;
            }
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
            ViewBag.IsAdmin = CurrentUserIsAdmin();
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Trabajador nuevoTrabajador, string password, string confirmPassword)
        {
            // 1. REVISIÓN DE VALIDACIÓN DEL FORMULARIO
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errores)
                {
                    System.Diagnostics.Debug.WriteLine("❌ ERROR DE VALIDACIÓN: " + error.ErrorMessage);
                }

                // Volvemos a mostrar la vista con el modelo para que el usuario corrija
                return View(nuevoTrabajador);
            }

            // Validar password si se envió desde el formulario y asignarlo a Clave (hasheado)
            if (!string.IsNullOrWhiteSpace(password) || !string.IsNullOrWhiteSpace(confirmPassword))
            {
                if (string.IsNullOrWhiteSpace(password)) { ModelState.AddModelError("password", "Contraseña obligatoria"); return View(nuevoTrabajador); }
                if (password.Length < 6) { ModelState.AddModelError("password", "La contraseña debe tener al menos 6 caracteres"); return View(nuevoTrabajador); }
                if (password != confirmPassword) { ModelState.AddModelError("confirmPassword", "Las contraseñas no coinciden"); return View(nuevoTrabajador); }

                // Hashear contraseña con BCrypt
                try
                {
                    nuevoTrabajador.Clave = BCrypt.Net.BCrypt.HashPassword(password);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error al hashear contraseña: " + ex.Message);
                    ModelState.AddModelError("password", "Error al procesar la contraseña.");
                    return View(nuevoTrabajador);
                }
            }

            try
            {
                using (var conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();

                    // 2. REVISIÓN DE CORREO REPETIDO
                    using (var check = new MySqlCommand("SELECT COUNT(*) FROM trabajadores WHERE correo=@correo", conexion))
                    {
                        check.Parameters.AddWithValue("@correo", nuevoTrabajador.Correo ?? string.Empty);
                        var cnt = Convert.ToInt32(check.ExecuteScalar());
                        if (cnt > 0)
                        {
                            ModelState.AddModelError("Correo", "Este correo ya se encuentra registrado en el sistema.");
                            return View(nuevoTrabajador);
                        }
                    }

                    // 3. INTENTO DE GUARDADO EN LA BASE DE DATOS
                    // Detectar si existe columna de contraseña en la tabla 'trabajadores'
                    var cols = new List<string>();
                    using (var ccmd = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='trabajadores'", conexion))
                    using (var r = ccmd.ExecuteReader()) { while (r.Read()) cols.Add(r.GetString(0)); }

                    var pwdCol = cols.FirstOrDefault(c => new[] { "password", "contrasena", "contrasenia", "pwd", "clave" }.Any(p => string.Equals(p, c, StringComparison.OrdinalIgnoreCase)));

                    string insertSql;
                    if (!string.IsNullOrEmpty(pwdCol) && !string.IsNullOrWhiteSpace(nuevoTrabajador.Clave))
                    {
                        insertSql = $"INSERT INTO trabajadores (nombre, apellido, correo, `{pwdCol}`) VALUES (@nombre, @apellido, @correo, @clave)";
                    }
                    else
                    {
                        insertSql = "INSERT INTO trabajadores (nombre, apellido, correo) VALUES (@nombre, @apellido, @correo)";
                    }

                    using (var cmd = new MySqlCommand(insertSql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@nombre", nuevoTrabajador.Nombre ?? string.Empty);
                        cmd.Parameters.AddWithValue("@apellido", nuevoTrabajador.Apellido ?? string.Empty);
                        cmd.Parameters.AddWithValue("@correo", nuevoTrabajador.Correo ?? string.Empty);
                        if (!string.IsNullOrEmpty(pwdCol) && !string.IsNullOrWhiteSpace(nuevoTrabajador.Clave)) cmd.Parameters.AddWithValue("@clave", nuevoTrabajador.Clave);
                        var affected = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"SQL INSERT affected rows: {affected}");
                        if (affected <= 0)
                        {
                            TempData["Error"] = "No se insertó ninguna fila en la base de datos.";
                            return View(nuevoTrabajador);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("✅ ¡Trabajador guardado con éxito!");
                TempData["Success"] = "Usuario creado correctamente.";
                return RedirectToAction("Index");

            }
            catch (Exception ex)
            {
                // 4. ATRAVEZANDO EL ERROR DE BASE DE DATOS
                string errorReal = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                System.Diagnostics.Debug.WriteLine("🔥 ERROR DE MYSQL/MARIADB: " + errorReal);

                ModelState.AddModelError("", "Error crítico al guardar en BD: " + errorReal);
                TempData["Error"] = "Error al crear usuario: " + errorReal;
                return View(nuevoTrabajador);
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(Trabajador modelo)
        {
            // 1. Truco: quitar la validación del ID si viene en el modelo
            ModelState.Remove("Id_trabajador");

            if (ModelState.IsValid)
            {
                // Si se recibió una clave en claro, hashearla (evitar doble-hash comprobando prefijo bcrypt)
                try
                {
                    if (!string.IsNullOrWhiteSpace(modelo.Clave) && !modelo.Clave.StartsWith("$2"))
                    {
                        modelo.Clave = BCrypt.Net.BCrypt.HashPassword(modelo.Clave);
                    }
                }
                catch { /* si falla el hash, continuar con el valor original */ }

                using (var conexion = new MySql.Data.MySqlClient.MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    // Insertar sin id (autoincremental)
                    var sql = "INSERT INTO trabajadores (nombre, apellido, correo, clave, admin) VALUES (@nombre, @apellido, @correo, @clave, @admin)";
                    using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@nombre", modelo.Nombre ?? string.Empty);
                        cmd.Parameters.AddWithValue("@apellido", modelo.Apellido ?? string.Empty);
                        cmd.Parameters.AddWithValue("@correo", modelo.Correo ?? string.Empty);
                        cmd.Parameters.AddWithValue("@clave", modelo.Clave ?? string.Empty);
                        cmd.Parameters.AddWithValue("@admin", modelo.Admin ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Index");
            }

            // 2. Si falla la validación, registrar errores para depuración
            foreach (var value in ModelState.Values)
            {
                foreach (var error in value.Errors)
                {
                    System.Diagnostics.Debug.WriteLine("🚨 BLOQUEADO POR: " + error.ErrorMessage);
                }
            }

            return View("Create", modelo);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // TestLogin DESACTIVADO: usado sólo durante depuración. Eliminado por petición del desarrollador.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string correo, string clave)
        {
            if (string.IsNullOrWhiteSpace(correo)) ModelState.AddModelError("correo", "Correo obligatorio");
            if (string.IsNullOrWhiteSpace(clave)) ModelState.AddModelError("clave", "Contraseña obligatoria");
            if (!ModelState.IsValid) return View();

            Dictionary<string, object> trabajador = null;
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM trabajadores WHERE correo=@correo LIMIT 1", conexion))
                {
                    cmd.Parameters.AddWithValue("@correo", correo);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            trabajador = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++) trabajador[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                    }
                }
            }

            if (trabajador == null)
            {
                ModelState.AddModelError("correo", "Usuario no encontrado");
                return View();
            }

            // Buscar la columna que contiene el hash de la contraseña
            var pwdCandidates = new[] { "Clave", "clave", "password", "contrasena", "contrasenia", "pwd", "clave_hash" };
            string storedHash = null;
            foreach (var c in pwdCandidates)
            {
                if (trabajador.ContainsKey(c) && trabajador[c] != null)
                {
                    storedHash = trabajador[c].ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(storedHash))
            {
                ModelState.AddModelError("clave", "Este usuario no tiene contraseña registrada.");
                return View();
            }

            bool verified = false;

            // Verificar usando BCrypt (requiere BCrypt.Net-Next)
            try
            {
                verified = BCrypt.Net.BCrypt.Verify(clave, storedHash);
            }
            catch
            {
                verified = false;
            }

            // Si no es bcrypt, comprobar formato PBKDF2 iter$salt$hash
            if (!verified)
            {
                var parts = storedHash?.Split('$');
                if (parts != null && parts.Length == 3 && int.TryParse(parts[0], out var iter))
                {
                    try
                    {
                        var salt = System.Convert.FromBase64String(parts[1]);
                        var key = System.Convert.FromBase64String(parts[2]);
                        using (var derive = new System.Security.Cryptography.Rfc2898DeriveBytes(clave, salt, iter, System.Security.Cryptography.HashAlgorithmName.SHA256))
                        {
                            var derived = derive.GetBytes(key.Length);
                            verified = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(derived, key);
                        }
                    }
                    catch { verified = false; }
                }
            }

            if (!verified)
            {
                ModelState.AddModelError("clave", "Contraseña incorrecta");
                return View();
            }

            // Login exitoso: establecer cookie simple (HttpOnly)
            var email = trabajador.ContainsKey("correo") ? trabajador["correo"]?.ToString() : correo;
            // Determinar id y nombre legible del trabajador para almacenarlos en cookies
            string workerId = null;
            string workerName = null;
            try
            {
                // Buscar columna id común
                var idCandidates = new[] { "id", "trabajador_id", "empleado_id", "user_id", "usuario_id", "id_trabajador" };
                var idCol = trabajador.Keys.FirstOrDefault(k => idCandidates.Any(p => string.Equals(p, k, System.StringComparison.OrdinalIgnoreCase)))
                            ?? trabajador.Keys.FirstOrDefault(k => k.EndsWith("_id", System.StringComparison.OrdinalIgnoreCase))
                            ?? trabajador.Keys.FirstOrDefault(k => string.Equals(k, "id", System.StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(idCol) && trabajador.ContainsKey(idCol) && trabajador[idCol] != null)
                {
                    workerId = trabajador[idCol].ToString();
                }

                // Buscar nombre (nombre + apellido si es posible)
                var nombreCol = trabajador.Keys.FirstOrDefault(k => string.Equals(k, "nombre", System.StringComparison.OrdinalIgnoreCase));
                var apellidoCol = trabajador.Keys.FirstOrDefault(k => string.Equals(k, "apellido", System.StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(nombreCol) && trabajador.ContainsKey(nombreCol) && trabajador[nombreCol] != null)
                {
                    workerName = trabajador[nombreCol].ToString();
                    if (!string.IsNullOrEmpty(apellidoCol) && trabajador.ContainsKey(apellidoCol) && trabajador[apellidoCol] != null)
                        workerName = workerName + " " + trabajador[apellidoCol].ToString();
                }
                else
                {
                    // alternativa: columna 'name' o 'usuario'
                    var altName = trabajador.Keys.FirstOrDefault(k => string.Equals(k, "name", System.StringComparison.OrdinalIgnoreCase))
                                  ?? trabajador.Keys.FirstOrDefault(k => string.Equals(k, "usuario", System.StringComparison.OrdinalIgnoreCase))
                                  ?? trabajador.Keys.FirstOrDefault(k => string.Equals(k, "user", System.StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(altName) && trabajador.ContainsKey(altName) && trabajador[altName] != null) workerName = trabajador[altName].ToString();
                }
            }
            catch { }

            Response.Cookies.Append("user_email", email ?? string.Empty, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Expires = DateTimeOffset.UtcNow.AddHours(8) });
            if (!string.IsNullOrEmpty(workerId)) Response.Cookies.Append("user_id", workerId, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Expires = DateTimeOffset.UtcNow.AddHours(8) });
            if (!string.IsNullOrEmpty(workerName)) Response.Cookies.Append("user_name", workerName, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Expires = DateTimeOffset.UtcNow.AddHours(8) });
            TempData["Success"] = "Inicio de sesión correcto";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            try
            {
                // Eliminar la cookie de sesión simple usada por la aplicación
                if (Request.Cookies.ContainsKey("user_email")) Response.Cookies.Delete("user_email");
                if (Request.Cookies.ContainsKey("user_id")) Response.Cookies.Delete("user_id");
                if (Request.Cookies.ContainsKey("user_name")) Response.Cookies.Delete("user_name");
            }
            catch { /* ignorar errores al borrar cookie */ }

            // Redirigir al formulario de login
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult TestTrabajadores()
        {
            try
            {
                int count = 0;
                using (var conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM trabajadores", conexion))
                    {
                        var r = cmd.ExecuteScalar();
                        if (r != null && r != DBNull.Value) count = Convert.ToInt32(r);
                    }
                }
                return Content($"trabajadores count: {count}");
            }
            catch (Exception ex)
            {
                return Content("Error TestTrabajadores: " + ex.Message);
            }
        }

        [HttpGet]
        public IActionResult TestBcrypt()
        {
            try
            {
                var sample = "Test123!";
                var hash = BCrypt.Net.BCrypt.HashPassword(sample);
                var ok = BCrypt.Net.BCrypt.Verify(sample, hash);
                // Mostrar resultado conciso
                var shortHash = hash != null && hash.Length > 16 ? hash.Substring(0, 16) + "..." : hash;
                return Content($"BCrypt installed: OK\nHash sample: {shortHash}\nVerify sample: {ok}");
            }
            catch (Exception ex)
            {
                return Content($"BCrypt test failed: {ex.GetType().Name} - {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            // Sólo administradores pueden acceder a la edición de usuarios
            if (!CurrentUserIsAdmin())
            {
                TempData["Error"] = "No autorizado. Solo administradores pueden editar usuarios.";
                return RedirectToAction("Index");
            }
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
            ViewBag.KeyColumn = keyCol;
            ViewBag.IsAdmin = CurrentUserIsAdmin();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, string correo, string? admin)
        {
            // Comprobar permiso admin antes de procesar
            if (!CurrentUserIsAdmin())
            {
                TempData["Error"] = "No autorizado. Solo administradores pueden editar usuarios.";
                return RedirectToAction("Index");
            }
            // Sólo se permite editar el correo
            if (string.IsNullOrWhiteSpace(correo)) ModelState.AddModelError("correo", "Correo obligatorio");
            if (!ModelState.IsValid) return Edit(id);

            var keyCol = GetPrimaryKeyColumn("trabajadores") ?? "id";
            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Comprobar si la tabla tiene columna 'admin'
                bool hasAdmin = false;
                using (var chk = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='trabajadores' AND COLUMN_NAME='admin'", conexion))
                {
                    var r = chk.ExecuteScalar();
                    hasAdmin = r != null && Convert.ToInt32(r) > 0;
                }

                if (hasAdmin)
                {
                    var adminVal = 0;
                    if (!string.IsNullOrEmpty(admin) && (admin == "1" || string.Equals(admin, "true", StringComparison.OrdinalIgnoreCase) || admin == "on")) adminVal = 1;

                    var sql = $"UPDATE trabajadores SET correo=@correo, admin=@admin WHERE `{keyCol}`=@id";
                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@correo", correo);
                        cmd.Parameters.AddWithValue("@admin", adminVal);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    var sql = $"UPDATE trabajadores SET correo=@correo WHERE `{keyCol}`=@id";
                    using (var cmd = new MySqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@correo", correo);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            // Sólo administradores pueden eliminar usuarios
            if (!CurrentUserIsAdmin())
            {
                TempData["Error"] = "No autorizado. Solo administradores pueden eliminar usuarios.";
                return RedirectToAction("Index");
            }
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
