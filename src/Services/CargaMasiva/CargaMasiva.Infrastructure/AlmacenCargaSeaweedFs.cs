using CargaMasiva.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CargaMasiva.Infrastructure;

/// <summary>Adaptador SeaweedFS del puerto de descarga que posee CargaMasiva.</summary>
public sealed class AlmacenCargaSeaweedFs(HttpClient http) : IAlmacenCarga
{
    private const string Esquema = "seaweed://";

    public async Task<Stream> DescargarAsync(string ruta, CancellationToken ct)
    {
        var rutaRelativa = ruta.StartsWith(Esquema, StringComparison.Ordinal)
            ? ruta[Esquema.Length..]
            : throw new ArgumentException($"Ruta de almacenamiento inválida: '{ruta}'.", nameof(ruta));

        using var respuesta = await http.GetAsync(rutaRelativa, HttpCompletionOption.ResponseHeadersRead, ct);
        respuesta.EnsureSuccessStatusCode();

        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, ct);
        memoria.Position = 0;
        return memoria;
    }
}

public static class AlmacenCargaSeaweedFsExtensiones
{
    public static IServiceCollection AddAlmacenCargaSeaweedFs(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var filer = configuracion["SeaweedFs:Filer"]
            ?? throw new InvalidOperationException("Falta SeaweedFs:Filer.");

        servicios.AddHttpClient<IAlmacenCarga, AlmacenCargaSeaweedFs>(cliente =>
        {
            cliente.BaseAddress = new Uri(filer.EndsWith('/') ? filer : filer + "/");
            cliente.Timeout = TimeSpan.FromMinutes(9);
        })
        .AddStandardResilienceHandler(opciones =>
        {
            opciones.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
            opciones.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(8);
        });

        return servicios;
    }
}