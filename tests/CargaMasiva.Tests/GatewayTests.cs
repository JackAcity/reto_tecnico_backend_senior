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
        var respuesta = await Cliente.PostAsJsonAsync("/auth/login", new
        {
            email = Environment.GetEnvironmentVariable("SEED_EMAIL") ?? "admin@reto.local",
            password = Environment.GetEnvironmentVariable("SEED_PASSWORD") ?? "Reto2026!"
        });

        respuesta.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        AccessToken = json.RootElement.GetProperty("accessToken").GetString()!;
    }

    public Task DisposeAsync()
    {
        Cliente.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// El borde: autenticación, autorización por permiso y rate limiting (§4.3, §3.2a).
/// Requiere el stack levantado — <c>docker compose up -d</c>.
/// </summary>
public sealed class GatewayTests(GatewayFixture fixture) : IClassFixture<GatewayFixture>
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
}
