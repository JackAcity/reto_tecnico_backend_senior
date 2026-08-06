using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CargaMasiva.Tests;

/// <summary>
/// Token compartido por toda la clase: el login tiene su propio rate limit por IP,
/// así que se pide una sola vez y antes de cualquier ráfaga.
/// </summary>
public sealed class GatewayFixture : IAsyncLifetime
{
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("GATEWAY_URL") ?? "http://localhost:8080";

    public HttpClient Cliente { get; } = new() { BaseAddress = new Uri(BaseUrl) };
    public string AccessToken { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var respuesta = await LoginConReintentoAsync(Cliente,
            Environment.GetEnvironmentVariable("SEED_EMAIL") ?? "admin@reto.local",
            Environment.GetEnvironmentVariable("SEED_PASSWORD") ?? "Reto2026!");

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        AccessToken = json.RootElement.GetProperty("accessToken").GetString()!;
    }

    /// <summary>
    /// El bucket de /auth/login es compartido por IP con /auth/refresh (Bloque 4),
    /// y Rafaga_SobreRutaAnonima_Termina429 lo agota A PROPÓSITO como parte de su
    /// propia prueba — efecto secundario que puede durar hasta 60s (ventana fija) y
    /// golpear a un login de otra corrida de test iniciada poco después. El rate
    /// limiter está haciendo bien su trabajo; el cliente de test es el que tiene
    /// que ser resiliente a su propio vecino ruidoso, con reintento acotado.
    /// </summary>
    public static async Task<HttpResponseMessage> LoginConReintentoAsync(HttpClient cliente, string email, string password)
    {
        var limite = DateTime.UtcNow.AddSeconds(75);
        while (true)
        {
            var respuesta = await cliente.PostAsJsonAsync("/auth/login", new { email, password });

            if (respuesta.StatusCode != HttpStatusCode.TooManyRequests || DateTime.UtcNow >= limite)
            {
                respuesta.EnsureSuccessStatusCode();
                return respuesta;
            }

            await Task.Delay(5000);
        }
    }

    public Task DisposeAsync()
    {
        Cliente.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// GatewayTests y CargasTests comparten esta colección a propósito: ambas usan
/// GatewayFixture (un login real), y GatewayTests además incluye
/// Rafaga_SobreRutaAnonima_Termina429, que agota a propósito el mismo bucket de
/// rate limit (por IP, compartido entre /auth/login y /auth/refresh — Bloque 4).
/// Sin esta colección, xUnit corre las clases en paralelo por defecto y esa
/// ráfaga puede tirar 429 al login de la otra clase a mitad de su fixture.
/// </summary>
[CollectionDefinition("Gateway")]
public sealed class ColeccionGateway : ICollectionFixture<GatewayFixture>;

/// <summary>
/// El borde: autenticación, autorización por permiso y rate limiting (§4.3, §3.2a).
/// Requiere el stack levantado — <c>docker compose up -d</c>.
/// </summary>
[Collection("Gateway")]
public sealed class GatewayTests(GatewayFixture fixture)
{
    private HttpRequestMessage ConToken(HttpMethod metodo, string ruta)
    {
        var peticion = new HttpRequestMessage(metodo, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.AccessToken);
        return peticion;
    }

    [Fact]
    public async Task Login_PasaPorElGateway_YDevuelveToken()
    {
        Assert.NotEmpty(fixture.AccessToken);
    }

    [Fact]
    public async Task Cargas_SinToken_Da401()
    {
        var respuesta = await fixture.Cliente.GetAsync("/cargas");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cargas_ConTokenFalsificado_Da401()
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/cargas");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "no.es.un.token");

        var respuesta = await fixture.Cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// Con permiso carga:masiva la petición atraviesa el borde y llega a Control.
    /// Mientras Control no exponga /cargas responderá 404, que ya prueba lo que
    /// interesa acá: no es 401 ni 403.
    /// </summary>
    [Fact]
    public async Task Cargas_ConTokenValido_AtraviesaElBorde()
    {
        var respuesta = await fixture.Cliente.SendAsync(ConToken(HttpMethod.Get, "/cargas"));

        Assert.NotEqual(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ServiciosInternos_SeAlcanzanSoloConToken()
    {
        var sinToken = await fixture.Cliente.GetAsync("/servicios/cargamasiva/health");
        var conToken = await fixture.Cliente.SendAsync(ConToken(HttpMethod.Get, "/servicios/cargamasiva/health"));

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, conToken.StatusCode);
    }

    /// <summary>§4.3 — el rate limiter es obligatorio; acá se lo hace disparar de verdad.</summary>
    [Fact]
    public async Task Rafaga_SobreRutaAnonima_Termina429()
    {
        HttpStatusCode ultimo = HttpStatusCode.OK;

        for (var intento = 0; intento < 20 && ultimo != HttpStatusCode.TooManyRequests; intento++)
        {
            var respuesta = await fixture.Cliente.PostAsJsonAsync("/auth/refresh", new { refreshToken = "token-invalido" });
            ultimo = respuesta.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }

    /// <summary>
    /// §3.2a de punta a punta, no solo PermisosDe en aislado: un usuario real,
    /// autenticado, sin el claim carga:masiva, debe ser rechazado por la policy
    /// del gateway con 403 — ni 401 (sí está autenticado) ni 200 (no tiene el
    /// permiso). El rol "consulta" lo siembra Auth al arrancar (Seed:ConsultaRol).
    /// </summary>
    [Fact]
    public async Task Cargas_ConUsuarioSinPermiso_Da403()
    {
        var login = await GatewayFixture.LoginConReintentoAsync(fixture.Cliente,
            Environment.GetEnvironmentVariable("SEED_CONSULTA_EMAIL") ?? "consulta@reto.local",
            Environment.GetEnvironmentVariable("SEED_CONSULTA_PASSWORD") ?? "Consulta2026!");
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var tokenSinPermiso = json.RootElement.GetProperty("accessToken").GetString();

        var peticion = new HttpRequestMessage(HttpMethod.Get, "/cargas");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSinPermiso);

        var respuesta = await fixture.Cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>§C12 — /auth/login tiene su propio techo, mucho menor que el de /cargas.</summary>
    [Fact]
    public async Task Login_ConCuerpoDemasiadoGrande_NoDa401()
    {
        var cuerpoEnorme = new { email = "x@x.com", password = new string('a', 10_000) };

        var respuesta = await fixture.Cliente.PostAsJsonAsync("/auth/login", cuerpoEnorme);

        // No debe comportarse como una credencial simplemente inválida (401):
        // el límite de tamaño de YARP corta antes de que la petición llegue a Auth.
        Assert.NotEqual(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
