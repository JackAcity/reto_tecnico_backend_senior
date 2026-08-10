## 1. Excepción de configuración

- [x] 1.1 Crear `ExcepcionDeConfiguracion` en `src/BuildingBlocks` (D1):
      `sealed class ExcepcionDeConfiguracion(string mensaje) : InvalidOperationException(mensaje)`
- [x] 1.2 `OpcionesRabbit.Validar` (`Mensajeria/Topologia.cs`) lanza
      `ExcepcionDeConfiguracion` en los 3 `throw` (Host/Usuario/Password)
- [x] 1.3 `ServicioAutenticacion.LlaveDeFirma` (`Auth.Api/Application/
      ServicioAutenticacion.cs`) lanza `ExcepcionDeConfiguracion`

> Nota de implementación: la task 1.1 original planeaba una interfaz
> `IExcepcionDeNegocio` implementada por `TransicionInvalidaException`.
> `dotnet build` reveló que `CargaMasiva.Domain` no referencia `BuildingBlocks`
> y que agregarlo metería `Microsoft.AspNetCore.App`/JWT/Serilog al proyecto
> más puro del reto. Se pivotó a marcar la excepción de configuración en vez
> de la de negocio — ver design.md D1/D2 actualizado.

## 2. GlobalExceptionHandler

- [x] 2.1 Agregar el case `ExcepcionDeConfiguracion => (500, "Error interno", false)`
      ANTES del case combinado `ArgumentException or InvalidOperationException`
      en el `switch` de `GlobalExceptionHandler` (`ServiceDefaults.cs`)
- [x] 2.2 Confirmar que `ArgumentException`, `InvalidOperationException`
      genérica, `UnauthorizedAccessException` y `KeyNotFoundException` no
      cambian de rama

## 3. Tests

- [x] 3.1 ~~Quitar el `InlineData(typeof(InvalidOperationException), 400)`~~ —
      no aplica con el diseño final: `InvalidOperationException` genérica
      sigue siendo 400 (contrato sin cambios), el `Theory` existente queda
      igual
- [x] 3.2 Test nuevo: `ExcepcionDeConfiguracion("Falta RabbitMq:Password.")` →
      500, `detail` no contiene `"RabbitMq"` ni `"Password"`
      (`ExcepcionDeConfiguracion_Da500_SinFiltrarElMensaje`)
- [x] 3.3 ~~Test con fake `IExcepcionDeNegocio`~~ — no aplica, la interfaz se
      descartó
- [x] 3.4 Test nuevo: `TransicionInvalidaException` real (no fake) →
      400, `detail` contiene "Transición inválida"
      (`TransicionInvalidaException_Real_Da400_ConSuMensaje`) — cierra el
      Requirement "InvalidOperationException de negocio (no marcada) sigue
      siendo 400" del spec
- [x] 3.5 Confirmar que `ArgumentException`/`UnauthorizedAccessException`/
      `KeyNotFoundException`/`InvalidOperationException` del `Theory`
      existente siguen en verde sin tocar sus aserciones

## 4. Validación final

- [x] 4.1 `dotnet build` de la solución completa — 14 proyectos, 0 errores
- [x] 4.2 `dotnet test` completo — 114/114 en verde (112 base + 2 nuevos)
