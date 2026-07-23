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

                // Detectar columna de categoría en productos
                var possibleProdCatCols = new[] { "categoria_id", "categ_id", "categoriaId", "categoria", "categ" };
                string prodCatCol = null;
                using (var check = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='productos'", conexion))
                using (var reader = check.ExecuteReader())
                {
                    var cols = new List<string>();
                    while (reader.Read()) cols.Add(reader.GetString(0));
                    prodCatCol = possibleProdCatCols.FirstOrDefault(c => cols.Any(x => string.Equals(x, c, System.StringComparison.OrdinalIgnoreCase)));
                }

                // Si existe columna de categoría y la tabla categ existe, realizar JOIN para obtener el nombre
                bool joined = false;
                if (!string.IsNullOrEmpty(prodCatCol))
                {
                    // Detectar columna clave de categ
                    string catKey = null;
                    using (var check2 = new MySqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='categ'", conexion))
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

                    // Detectar etiqueta de categ (nombre)
                    string catLabel = null;
                    using (var cmdLabel = new MySqlCommand("SELECT * FROM categ LIMIT 1", conexion))
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

                    if (!string.IsNullOrEmpty(catKey) && !string.IsNullOrEmpty(catLabel))
                    {
                        // Intentar ejecutar la consulta JOIN adaptada al proyecto (selección explícita de columnas)
                        try
                        {
                            // Selección usando JOIN dinámico para incluir el nombre de la categoría como 'nombre_categoria'
                            var sql = $"SELECT p.*, c.`{catLabel}` AS nombre_categoria FROM productos p INNER JOIN categ c ON p.`{prodCatCol}` = c.`{catKey}`";
                            using (var cmd = new MySqlCommand(sql, conexion))
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

                // Construir INSERT adaptado: incluir columna id de categoría y opcionalmente categoria_nom
                string query;
                if (hasCategoriaNom && !string.IsNullOrEmpty(prodCatCol))
                {
                    query = $"INSERT INTO productos (nombre, stock, precio, `{prodCatCol}`, `categoria_nom`) VALUES (@nombre, @stock, @precio, @categoria, @categoria_nom)";
                }
                else if (!string.IsNullOrEmpty(prodCatCol))
                {
                    query = $"INSERT INTO productos (nombre, stock, precio, `{prodCatCol}`) VALUES (@nombre, @stock, @precio, @categoria)";
                }
                else
                {
                    query = "INSERT INTO productos (nombre, stock, precio) VALUES (@nombre, @stock, @precio)";
                }

                using (var cmd = new MySqlCommand(query, conexion))
                {
                    cmd.Parameters.AddWithValue("@nombre", nombre);
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
                    cmd.ExecuteNonQuery();
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
        public IActionResult Update(string keyColumn, string idValue, int stock)
        {
            if (string.IsNullOrEmpty(keyColumn) || string.IsNullOrEmpty(idValue))
                return RedirectToAction("Index");

            using (var conexion = new MySqlConnection(_connectionString))
            {
                conexion.Open();
                // Obtener valor de stock anterior para registrar historial
                int oldStockVal = -1;
                using (var getCmd = new MySqlCommand($"SELECT stock FROM productos WHERE `{keyColumn}`=@id", conexion))
                {
                    getCmd.Parameters.AddWithValue("@id", idValue);
                    var scalar = getCmd.ExecuteScalar();
                    if (scalar != null && scalar != System.DBNull.Value)
                    {
                        int.TryParse(scalar.ToString(), out oldStockVal);
                    }
                }

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

                    // Si no se encuentra columna para producto o fecha, no insertar (requerido según especificación)
                    if (!string.IsNullOrEmpty(prodCol) && !string.IsNullOrEmpty(dateCol))
                    {
                        using (var cmdIns = new MySqlCommand($"INSERT INTO registros (`{prodCol}`, `{dateCol}`) VALUES (@prodId, @fecha)", conexion))
                        {
                            cmdIns.Parameters.AddWithValue("@prodId", idValue);
                            cmdIns.Parameters.AddWithValue("@fecha", System.DateTime.Now);
                            cmdIns.ExecuteNonQuery();
                        }
                    }
                    else if (!string.IsNullOrEmpty(prodCol))
                    {
                        // Si sólo existe columna producto, insertar solo el id
                        using (var cmdIns = new MySqlCommand($"INSERT INTO registros (`{prodCol}`) VALUES (@prodId)", conexion))
                        {
                            cmdIns.Parameters.AddWithValue("@prodId", idValue);
                            cmdIns.ExecuteNonQuery();
                        }
                    }
                    else if (!string.IsNullOrEmpty(dateCol))
                    {
                        // Si sólo existe columna fecha, insertar sólo la fecha
                        using (var cmdIns = new MySqlCommand($"INSERT INTO registros (`{dateCol}`) VALUES (@fecha)", conexion))
                        {
                            cmdIns.Parameters.AddWithValue("@fecha", System.DateTime.Now);
                            cmdIns.ExecuteNonQuery();
                        }
                    }
                }
            }

            return RedirectToAction("Index");
        }
    }
}
