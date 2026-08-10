## 1. Resultado<T> compartido

- [x] 1.1 Crear `Resultado<T>` y `Resultado` en `src/BuildingBlocks` (D3)
- [x] 1.2 Test unitario de `Resultado<T>`: `Exito`/`Fallo` exponen `EsExitoso`,
      `Valor`, `Error` correctamente en cada caso

## 2. Puerto de datos de CargaMasiva

- [x] 2.1 Definir `IRepositorioCargas` en `CargaMasiva.Application` (D1)
- [x] 2.2 Implementar el adaptador EF en `CargaMasiva.Infrastructure`
- [x] 2.3 Cambiar el constructor de `ManejadorCarga` para depender de
      `IRepositorioCargas` en vez de `RetoDbContext`
- [x] 2.4 Actualizar el registro DI en `CargaMasiva.Api/Program.cs`
- [x] 2.5 Test: `ManejadorCarga` con un fake de `IRepositorioCargas` (sin
      `DbContext` real) reproduce los casos ya cubiertos por
      `ProcesadorLoteTests`/`RegistroDeCargaTests`

## 3. Capas en Auth.Api

- [x] 3.1 Crear `Application/` e `Infrastructure/` en `Auth.Api`
- [x] 3.2 Definir `IRepositorioUsuarios` (Application) e implementarlo sobre
      `RetoDbContext` (Infrastructure) — D2
- [x] 3.3 Mover `ServicioAutenticacion` a `Application/`, cambiar su
      constructor para depender de `IRepositorioUsuarios`
- [x] 3.4 Actualizar registro DI en `Auth.Api/Program.cs`
- [x] 3.5 Confirmar que `AutenticacionTests` (login, rotación, timing-attack)
      sigue pasando sin modificar sus aserciones

## 4. Capas en Control.Api

- [x] 4.1 Crear `Application/` e `Infrastructure/` en `Control.Api`
- [x] 4.2 Definir `IRepositorioCargas` (comando) e `IConsultaCargas` (lectura)
      en Application; implementarlos en Infrastructure — D2
- [x] 4.3 Mover `ServicioCargas` y `ConsultaCargas` a `Application/`, cambiar
      sus constructores para depender de los puertos nuevos
- [x] 4.4 Actualizar registro DI en `Control.Api/Program.cs`
- [x] 4.5 Confirmar que `CargasTests`/`RegistroDeCargaTests` siguen pasando
      sin modificar sus aserciones

## 5. Capas en Notificaciones.Api

- [x] 5.1 Crear `Application/` e `Infrastructure/` en `Notificaciones.Api`
- [x] 5.2 Definir `IRepositorioNotificaciones` (Application), implementarlo en
      Infrastructure — D2
- [x] 5.3 Mover `ManejadorNotificacion` a `Application/`, cambiar su
      constructor para depender de `IRepositorioNotificaciones`
- [x] 5.4 Actualizar registro DI en `Notificaciones.Api/Program.cs`
- [x] 5.5 Confirmar que los tests existentes de notificación siguen pasando
      sin modificar sus aserciones

## 6. Resultado<T> en IPublicador y casos de uso

- [x] 6.1 Cambiar `IPublicador.PublicarAsync` de `Task` a `Task<Resultado>`
      (D4)
- [x] 6.2 Actualizar la implementación sobre RabbitMQ para capturar el fallo
      de publicación y devolver `Resultado.Fallo(...)` en vez de lanzar
- [x] 6.3 `ServicioCargas.RegistrarAsync`: reemplazar el `catch (Exception ex)`
      genérico por una rama explícita sobre `resultado.EsExitoso`
- [x] 6.4 `ManejadorCarga.ProcesarAsync`: cambiar el retorno a
      `Task<Resultado<EstadoCarga>>`; la publicación final a notificación pasa
      a loguear con `LogWarning` en vez de dejar subir la excepción (D4)
- [x] 6.5 Test: fallo simulado de `IPublicador` en `RegistrarAsync` deja la
      carga en `Fallida` sin pasar por ningún `catch`
- [x] 6.6 Test: un fallo no relacionado con publicación (excepción inesperada
      dentro de `IPublicador`) se sigue propagando sin capturarse

## 7. Guardia de arquitectura

- [x] 7.1 Crear `Reto.Tests/GuardiaArquitecturaTests.cs` (proyecto ya
      renombrado por `saneamiento-proyecto-tests`)
- [x] 7.2 Caso CargaMasiva: reflection sobre `CargaMasiva.Application.dll` y
      `CargaMasiva.Domain.dll` contra `Microsoft.EntityFrameworkCore`,
      `Npgsql`, `RabbitMQ.Client` (D5)
- [x] 7.3 Caso Auth/Control/Notificaciones: escaneo de texto de los `.cs` bajo
      `*/Application/` contra los mismos tres `using` prohibidos (D5)
- [x] 7.4 Confirmar que la guardia falla si se reintroduce a mano una
      referencia prohibida (probarlo localmente y revertir), y pasa en verde
      con el estado final del repo

## 8. Validación final

- [x] 8.1 `dotnet build` de la solución completa — 14 proyectos, 0 errores
- [x] 8.2 `dotnet test` completo — 112/112 en verde (99 base + 13 nuevos:
      Resultado, ManejadorCarga con fakes, propagación de bug, guardia de
      arquitectura)
- [x] 8.3 `Impact` actualizado: faltaba `src/Shared/Mensajeria/Publicador.cs`
      (consecuencia de §D4) y el nombre `Result<T>` corregido a `Resultado`
      (decidido en §D3, no reflejado en la versión original del proposal)
