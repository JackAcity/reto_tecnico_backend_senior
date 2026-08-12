using Npgsql;

var cadenaConexion = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

var rutaEsquema = Path.Combine(AppContext.BaseDirectory, "esquema.sql");
if (!File.Exists(rutaEsquema))
    throw new FileNotFoundException("No se encontró el esquema SQL publicado.", rutaEsquema);

var esquema = await File.ReadAllTextAsync(rutaEsquema);
await using var conexion = new NpgsqlConnection(cadenaConexion);
await conexion.OpenAsync();
await using var comando = new NpgsqlCommand(esquema, conexion);
await comando.ExecuteNonQueryAsync();

Console.WriteLine("Esquema de base de datos aplicado correctamente.");