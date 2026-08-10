## Context

La auditoría `dotnet-audit` sobre el repo completo confirmó las 3 desviaciones
reales que el proposal ya identificaba, con evidencia concreta de código:

- `CargaMasiva.Application/ManejadorCarga.cs:32` depende del constructor de
  `RetoDbContext` (EF concreto), no de un puerto — único caso de acceso a
  datos sin puerto en el reto.
- `Auth.Api/ServicioAutenticacion.cs`, `Control.Api/ServicioCargas.cs`,
  `Control.Api/ConsultaCargas.cs` y `Notificaciones.Api/ManejadorNotificacion.cs`
  dependen todos directo de `RetoDbContext`, sin capa Application/Domain
  declarada donde trazar el límite.
- Ningún caso de uso devuelve un resultado de negocio explícito. Evidencia
  puntual: `Control.Api/ServicioCargas.cs:96` — `catch (Exception ex)` alrededor
  de la publicación a RabbitMQ atrapa *cualquier* excepción, no solo el fallo
  de infraestructura esperado; un bug real en `IPublicador` se reportaría como
  "carga registrada pero no encolada" en vez de propagarse.

No existe hoy ninguna guardia automática que impida que este tipo de
desviación vuelva a aparecer en un servicio nuevo.

## Goals / Non-Goals

**Goals:**
- Cerrar el único gap de DIP en `CargaMasiva.Application` con un puerto
  angosto (ISP).
- Dar a Auth, Control y Notificaciones una separación de capas real donde su
  lógica ya lo justifica (los 3 tienen reglas de negocio no triviales:
  mitigación de timing attack + rotación CAS en Auth; validación de archivo +
  dual-write en Control; idempotencia + transición de estado en Notificaciones).
- Introducir `Resultado<T>` para fallos de negocio esperados, reservando
  excepciones para lo verdaderamente excepcional.
- Agregar una guardia de arquitectura determinística que falle el build si
  Application/Domain de cualquier servicio referencia infraestructura
  concreta.

**Non-Goals:**
- No se duplica el modelo EF: `CargaArchivo`, `Usuario` y `RefreshToken` se
  trasladan a los proyectos Domain que los poseen; `Persistencia` conserva
  únicamente el mapeo y depende hacia esos dominios. No se introduce un
  segundo modelo ni un mapeador artificial.
- No se resuelve el fallo latente ya presente hoy en `ManejadorCarga` cuando
  la publicación *final* a la cola de notificación falla después de que la
  carga ya transicionó a `Finalizado` (un reintento de RabbitMQ vuelve a
  entrar y la guarda de idempotencia lo ignora en silencio, sin republicar).
  Es un bug preexistente fuera del alcance de las 4 capabilities de este
  change — se documenta como Open Question, no se arregla de paso.
- No se toca el mecanismo de reintentos/DLQ de RabbitMQ ni la máquina de
  estados (`maquina-estados.md`, ya especificada en `carga-masiva-microservicios`).

## Decisions

### D1 — Puerto de datos de `ManejadorCarga`: `IRepositorioCargas`

Puerto angosto en `CargaMasiva.Application`, implementado en
`CargaMasiva.Infrastructure` sobre el `RetoDbContext` ya existente (mismo
`DbContext` con scope por mensaje, solo detrás de una interfaz):

```csharp
public interface IRepositorioCargas
{
    Task<CargaArchivo> ObtenerAsync(int idCarga, CancellationToken ct);
    Task<IReadOnlyList<CargaPeriodo>> ObtenerPeriodosAsync(int idCarga, CancellationToken ct);
    void AgregarErrores(IEnumerable<DetalleCargaError> errores);
    Task GuardarCambiosAsync(CancellationToken ct);
}
```

Mapeo 1:1 con el uso actual dentro de `ManejadorCarga.ProcesarAsync`:
`db.CargaArchivos.SingleAsync` → `ObtenerAsync`; `db.CargaPeriodos.Where(...)`
→ `ObtenerPeriodosAsync`; `db.DetalleCargaErrores.AddRange(...)` →
`AgregarErrores`; los dos `db.SaveChangesAsync(ct)` → `GuardarCambiosAsync`.
`carga.Transicionar(...)` sigue siendo un método de dominio sobre la entidad
ya cargada — no necesita puerto, el cambio se persiste en el siguiente
`GuardarCambiosAsync` porque la implementación mantiene el mismo `DbContext`
con tracking activo (repositorio clásico sobre EF, no Unit of Work nuevo).

**Alternativa descartada**: exponer `IQueryable<CargaArchivo>` en el puerto
(más flexible) — se descarta porque filtra un detalle de EF (`IQueryable`) a
través de la abstracción, exactamente lo que el puerto existe para evitar.

### D2 — Límites de capa y propietarios de entidades

`CargaMasiva.Domain` y `Auth.Domain` son proyectos puros porque sus entidades
son consumidas por puertos de Application y por el adaptador EF; así el flujo
de referencias es `Infraestructura -> Domain`, nunca el inverso. No se duplica
el modelo: `Persistencia` mapea las mismas entidades y pasa a depender de esos
proyectos Domain.

Auth, Control y Notificaciones tienen también un proyecto `*.Application`.
Para reducir movimientos mecánicos, sus archivos permanecen bajo la carpeta
`Application/` del servicio y se incluyen desde ese proyecto; el ejecutable los
excluye de su propia compilación. El resultado es una frontera de ensamblado
real: los casos de uso sólo ven sus propios puertos, contratos de
BuildingBlocks y Domain. La composición y los adaptadores concretos quedan en
el ejecutable (anillo exterior), que depende hacia Application.

| Servicio | Domain | Application/ | Infrastructure/ |
|---|---|---|---|
| Auth.Api | `Auth.Domain`: `Usuario`, `RefreshToken` | `ServicioAutenticacion`, `IRepositorioUsuarios`, `IProtectorContrasenas`, `IEmisorAccessToken` | Repositorio EF, `ProtectorContrasenas`, `EmisorJwt` |
| Control.Api | `CargaMasiva.Domain` | `ServicioCargas`, `ConsultaCargas`, puertos de repositorio, almacenamiento y publicación | Adaptadores EF, SeaweedFS y RabbitMQ |
| Notificaciones.Api | `CargaMasiva.Domain` | `ManejadorNotificacion`, `IRepositorioNotificaciones` | Repositorio EF y correo |

`Endpoints/` no se introduce todavía en ninguno de los 3 — los 3 siguen con
≤10 rutas mapeadas en `Program.cs` (§3 de `dotnet-clean-style`); introducir la
carpeta ahora sería estructura sin necesidad (YAGNI).

### D3 — Nombre del tipo: `Resultado<T>`, no `Result<T>`

El proposal dejaba el naming abierto. El propio código ya tiene el patrón
establecido en español para DTOs de salida de caso de uso —
`ResultadoRegistro`, `ResultadoAutenticacion`, `ResultadoPeriodo` — así que
`Result<T>` en inglés rompería la convención bilingüe existente (§4 de
`dotnet-clean-style`: español para lo que tiene significado de negocio). Se
nombra `Resultado<T>`:

```csharp
public sealed class Resultado<T>
{
    public bool EsExitoso { get; }
    public T Valor { get; }
    public string? Error { get; }

    public static Resultado<T> Exito(T valor) => new(true, valor, null);
    public static Resultado<T> Fallo(string error) => new(false, default, error);
}

public sealed class Resultado   // variante sin valor, para operaciones sin retorno útil
{
    public bool EsExitoso { get; }
    public string? Error { get; }

    public static Resultado Exito() => new(true, null);
    public static Resultado Fallo(string error) => new(false, error);
}
```

Vive en `src/BuildingBlocks`, que queda como núcleo compartido sin paquetes ni
referencias de framework. La configuración HTTP, JWT, Serilog y el manejador
global de excepciones viven en `src/ServiceHost`: son composición de host, no
una dependencia del núcleo.

### D4 — Dónde se aplica `Resultado<T>`: puertos de publicación y `ManejadorCarga`

Los puertos de publicación viven en cada Application y se expresan en términos
del mensaje que cada caso de uso necesita; los adaptadores RabbitMQ viven fuera
de Application. El cliente técnico de RabbitMQ normaliza solo su fallo esperado
para que cada adaptador local lo traduzca a `Resultado`. Esto cierra el hallazgo
concreto de la auditoría:

- **`ServicioCargas.RegistrarAsync`**: `IPublicadorCargas` entrega un resultado
  explícito al caso de uso; una rama sobre `resultado.EsExitoso` conserva el
  mismo
  comportamiento observable (carga queda `Fallida`, se devuelve
  `ResultadoRegistro` con `Error`), pero ya no puede confundir un bug real
  dentro del adaptador de publicación con un fallo esperado de infraestructura — un bug
  ahora sí se propaga como excepción no controlada.
- **`ManejadorCarga.ProcesarAsync`** (publicación final a la cola de
  notificación): pasa de "dejar que la excepción suba y RabbitMQ reintente"
  a "loguear el fallo con `log.LogWarning` y retornar normalmente" — la carga
  ya es `Finalizado` en este punto, y el reintento actual no hacía nada útil
  de todas formas (ver Non-Goals: el bug preexistente de la guarda de
  idempotencia). Este es un cambio de comportamiento observable, documentado
  acá explícitamente, no incidental.
- `ManejadorCarga.ProcesarAsync` en sí pasa de `Task` a `Task<Resultado<EstadoCarga>>`
  — comunica el estado terminal (`Rechazada`/`Bloqueada`/`Finalizado`) sin que
  el llamador (o un test) tenga que releer la base para saber qué pasó.
  `ConsumidorCargaMasiva` sigue actuando igual (ack siempre que no haya
  excepción); el valor de retorno se usa solo para logging y para los tests
  nuevos de esta capability.

### D5 — Guardia de arquitectura: dos técnicas, una por tipo de límite

CargaMasiva tiene capas como **proyectos separados** (`.csproj` distintos) →
`Assembly.GetReferencedAssemblies()` sobre `CargaMasiva.Application.dll` y
`CargaMasiva.Domain.dll` detecta una referencia a `Microsoft.EntityFrameworkCore`,
`Npgsql` o `RabbitMQ.Client` con reflection real.

`Auth.Domain` y los cuatro proyectos `*.Application` se inspeccionan como
ensamblados puros: reflection comprueba que no referencien EF, Npgsql o
RabbitMQ. Además la guardia inspecciona sus `.csproj` y falla si alguno
referencia los adaptadores compartidos `Almacenamiento`, `Mensajeria` o
`Persistencia`. El escaneo de fuente bajo `*/Application/` se conserva como
defensa legible: prohíbe esos espacios de adaptadores y, en Auth, Identity,
Options y los paquetes JWT.

Un solo archivo de test (`Reto.Tests/GuardiaArquitecturaTests.cs`, ya en el
proyecto renombrado por `saneamiento-proyecto-tests`) corre ambas técnicas,
una por servicio según su tipo de límite.

## Risks / Trade-offs

- [Riesgo] El repositorio EF-backed (D1, D2) sigue dependiendo de que el
  `DbContext` inyectado sea el mismo a lo largo de un scope para que
  `Transicionar()` + `GuardarCambiosAsync()` funcionen sin pasar la entidad de
  vuelta explícitamente. → Mitigación: los repositorios se registran
  `Scoped`, igual que `RetoDbContext` ya lo está hoy; ningún cambio de
  lifetime.
- [Riesgo] Los puertos semánticos de publicación (`IPublicadorCargas` e
  `IPublicadorNotificacion`) obligan a adaptar una implementación nueva de
  broker. → Mitigación: cada adaptador traduce el único fallo técnico esperado
  a `Resultado`; los bugs no se capturan como fallo de negocio y se propagan.

## Migration Plan

1. `Resultado<T>`/`Resultado` en `BuildingBlocks` (D3) — sin consumidores
   todavía, cero riesgo de romper algo existente.
2. `IRepositorioCargas` + adaptador EF en CargaMasiva (D1) — service aislado,
   se valida con sus propios tests antes de tocar los otros 3 servicios.
3. Capas en Auth/Control/Notificaciones (D2) — un servicio a la vez, cada uno
   compila y sus tests existentes siguen pasando sin cambios de comportamiento.
4. Puertos de publicación semánticos → `Resultado` (D4) — el cambio de firma
   que más superficie toca, va después de que los repositorios (1-3) ya
   existen, así la rama de manejo de fallo puede usar los mismos puertos.
5. Guardia de arquitectura (D5) — al final: valida el estado final de 1-4 y
   queda corriendo para cualquier servicio futuro.

Rollback: cada paso es un commit independiente; revertir el último paso que
falló no arrastra a los anteriores (no hay dependencia hacia atrás).

## Open Questions

- El bug preexistente de la guarda de idempotencia (Non-Goals) — ¿se abre un
  hallazgo aparte para `carga-masiva-microservicios` o se deja como deuda
  documentada sin change todavía? No se decide acá; se marca para
  `dotnet-audit` en una próxima pasada.
