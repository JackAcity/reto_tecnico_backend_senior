## Context

`GlobalExceptionHandler` (`src/BuildingBlocks/ServiceDefaults.cs`) clasifica por
tipo runtime exacto:

```csharp
var (status, titulo, exponerDetalle) = ex switch
{
    ArgumentException or InvalidOperationException => (400, "Solicitud inválida", true),
    UnauthorizedAccessException => (403, "Acceso denegado", true),
    KeyNotFoundException => (404, "Recurso no encontrado", true),
    _ => (500, "Error interno", false)
};
```

`InvalidOperationException` tiene hoy dos orígenes que no deberían compartir
rama: `TransicionInvalidaException` (`CargaMasiva.Domain/EstadoCarga.cs`,
`: InvalidOperationException`, mensaje pensado para el cliente) y validación
de configuración faltante (`Topologia.Validar`, `ServicioAutenticacion.
LlaveDeFirma`, `OpcionesSmtp.Validar`, `AlmacenSeaweedFs`, los `Requerido(...)`
de los `Program.cs`) — mensaje NO pensado para el cliente, hoy expuesto igual.

`BuildingBlocks` es referenciado por los 5 servicios (dependencia hacia abajo,
igual que `Resultado`/`Resultado<T>`); no puede referenciar tipos concretos de
un servicio (`CargaMasiva.Domain`) sin invertir esa dirección.

## Goals / Non-Goals

**Goals:**
- El handler distingue "excepción de negocio pensada para el cliente" de
  "excepción de configuración/infraestructura" sin importar que ambas hoy
  compartan el tipo runtime `InvalidOperationException`.
- Cualquier `InvalidOperationException` "pelada" (sin marca explícita) cae en
  la rama 500 sin exponer `ex.Message` — mismo tratamiento que hoy ya reciben
  las excepciones no clasificadas.
- `TransicionInvalidaException` mantiene su contrato observable actual: 400,
  mensaje expuesto.

**Non-Goals:**
- No se introduce una jerarquía nueva de excepciones de negocio para todo el
  repo — alcance acotado a resolver la colisión encontrada. Si aparece otra
  excepción de negocio real que necesite el mismo tratamiento, se agrega al
  mecanismo elegido acá, no se abre una jerarquía paralela por servicio.
- No se toca la clasificación de `ArgumentException`, `UnauthorizedAccessException`
  ni `KeyNotFoundException` — no hay evidencia de la misma colisión ahí.
- No se resuelve con un catálogo de "excepciones de config conocidas": listar
  tipos malos escala peor que marcar el tipo bueno (ver Decisions).

## Decisions

### D1 — Marcar la excepción de configuración, no la de negocio

Alternativas consideradas:

1. **Interfaz marcadora en la excepción de negocio** (`IExcepcionDeNegocio` en
   `BuildingBlocks`, implementada por `TransicionInvalidaException`).
   Descartada **durante la implementación**: `CargaMasiva.Domain.csproj` no
   referencia `BuildingBlocks` (0 `ProjectReference` hoy) y `BuildingBlocks`
   trae `FrameworkReference Microsoft.AspNetCore.App` + `Microsoft.AspNetCore.
   Authentication.JwtBearer` + `Serilog.AspNetCore` (`BuildingBlocks.csproj`) —
   agregar la referencia metería el framework web completo al proyecto más
   puro del reto, exactamente la violación de DIP que este change corrige en
   otro punto. La premisa original ("`CargaMasiva.Domain` ya referencia
   `BuildingBlocks` igual que `Resultado<T>`") era falsa: `Resultado<T>` lo
   consume `CargaMasiva.Application` (sí referencia `BuildingBlocks`), no
   `Domain`. `dotnet build` lo confirmó con `CS0246` al primer intento.
2. **Excepción marcadora `ExcepcionDeConfiguracion`** en `BuildingBlocks`,
   usada en los sitios que lanzan config faltante. El handler la matchea
   *antes* del case combinado de `InvalidOperationException`; todo lo demás
   (incluida `TransicionInvalidaException`, sin tocar) sigue el contrato
   actual sin cambios.

   ```csharp
   namespace BuildingBlocks;

   public sealed class ExcepcionDeConfiguracion(string mensaje) : InvalidOperationException(mensaje);
   ```

Se elige la opción 2: cero cambios a `CargaMasiva.Domain`, cero project
references nuevas en ningún proyecto — de los ~9 sitios que hoy lanzan
`InvalidOperationException` por config faltante, solo 2 son alcanzables
durante un request HTTP en curso (ver alcance abajo), y ambos ya referencian
`BuildingBlocks`: `Topologia.Validar` (`Mensajeria`, `ProjectReference` a
`BuildingBlocks.csproj` ya declarada — la usa para `Resultado`/`Resultado<T>`
en `Publicador.cs`) y `ServicioAutenticacion.LlaveDeFirma` (`Auth.Api`,
`ProjectReference` a `BuildingBlocks.csproj` ya declarada). El switch del
handler pasa a:

```csharp
var (status, titulo, exponerDetalle) = ex switch
{
    ExcepcionDeConfiguracion => (StatusCodes.Status500InternalServerError, "Error interno", false),
    ArgumentException or InvalidOperationException => (StatusCodes.Status400BadRequest, "Solicitud inválida", true),
    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acceso denegado", true),
    KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado", true),
    _ => (StatusCodes.Status500InternalServerError, "Error interno", false)
};
```

### D2 — Alcance: solo los 2 sitios alcanzables por HTTP, no los ~9 totales

De los sitios que lanzan `InvalidOperationException` por config faltante:
`Topologia.Validar` (dentro de `PublicadorRabbit.CanalAsync`, llamado desde
`ServicioCargas.RegistrarAsync` — `POST /cargas`) y `ServicioAutenticacion.
LlaveDeFirma` (`POST /auth/login`) corren **dentro** de un request HTTP en
curso → llegan a `GlobalExceptionHandler`. Los demás (`Requerido()` en los 4
`Program.cs`, `AlmacenSeaweedFs`'s `AddAlmacenamiento`, `OpcionesSmtp.Validar`,
las validaciones dentro de `ConsumidorNotificaciones`/`ConsumidorCargaMasiva.
ExecuteAsync`) corren **antes de `app.Run()`** o dentro de un
`BackgroundService` — el proceso no arranca o el mensaje se reintenta/DLQ, en
ningún caso pasan por el middleware HTTP. Convertirlos hoy sería alcance sin
evidencia de bug (ninguno reproduce el hallazgo de la auditoría) y, para
`AlmacenSeaweedFs` en particular, exigiría agregar una `ProjectReference` a
`BuildingBlocks` en `Almacenamiento` — el mismo tipo de acoplamiento nuevo que
D1 evita para `CargaMasiva.Domain`. Se documenta como límite conocido, no se
resuelve acá (YAGNI) — si alguno de esos sitios se vuelve alcanzable por HTTP
en el futuro (ej. una validación de config se mueve a lazy/per-request), este
mismo mecanismo aplica sin cambios adicionales al handler.

## Risks / Trade-offs

- [Riesgo] Fail-open residual: cualquier `InvalidOperationException` de
  configuración *nueva* que alguien agregue en el futuro (en un sitio ya
  alcanzable por HTTP, o uno que se vuelva alcanzable) sigue cayendo en 400
  con `ex.Message` expuesto si no usa `ExcepcionDeConfiguracion` explícitamente
  — el mismo patrón de bug que motivó este change, no eliminado de raíz. Se
  aceptó a cambio de no introducir dependencias nuevas entre proyectos (D1/D2).
  → Mitigación: `GlobalExceptionHandlerTests.cs` documenta el contrato con un
  test explícito por tipo; el nombre `ExcepcionDeConfiguracion` es
  autoexplicativo en el sitio de lanzamiento; el comentario en el `switch` de
  `GlobalExceptionHandler` señala el criterio para quien agregue un case
  nuevo.
- [Riesgo] Los 7 sitios de config faltante no convertidos (D2) siguen
  lanzando `InvalidOperationException` pelada. Si alguno se vuelve alcanzable
  por HTTP más adelante (ej. se mueve de `Program.cs`/`BackgroundService` a
  un caso de uso), volvería a exponer su mensaje como 400 hasta que se
  convierta explícitamente. → Mitigación aceptada, documentada acá; no hay
  guardia automática para esto (correspondería a un `dotnet-audit` de
  seguimiento si crece el número de sitios).
- [Riesgo] Cobertura incompleta: si existe algún otro lanzamiento de
  `InvalidOperationException` pensado como negocio real (no encontrado en la
  auditoría de esta sesión) no cambia de comportamiento — sigue 400 con
  mensaje expuesto, sin regresión. → Sin mitigación necesaria: el diseño
  preserva el contrato previo para todo lo que no sea `ExcepcionDeConfiguracion`.
