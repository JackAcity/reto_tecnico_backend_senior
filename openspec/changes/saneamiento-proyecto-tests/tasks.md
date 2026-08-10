## 1. Rename del proyecto

- [x] 1.1 `git mv tests/CargaMasiva.Tests tests/Reto.Tests`
- [x] 1.2 `git mv tests/Reto.Tests/CargaMasiva.Tests.csproj tests/Reto.Tests/Reto.Tests.csproj`
- [x] 1.3 Reemplazar `namespace CargaMasiva.Tests;` → `namespace Reto.Tests;` en
      todos los `.cs` de `tests/Reto.Tests/`
- [x] 1.4 Eliminar `tests/Reto.Tests/UnitTest1.cs`
- [x] 1.5 Actualizar la referencia del proyecto (ruta y nombre) — CORRECCIÓN:
      no existe `reto_tecnico_backend_senior.sln` como asumía el design; el
      repo usa `Reto.slnx` (formato XML nuevo). Se actualizó ahí. También se
      corrigieron referencias en `README.md` y `docs/pruebas-de-escala.md`
      (`dotnet test tests/CargaMasiva.Tests` → `tests/Reto.Tests`), y en el
      proposal.md de `arquitectura-hexagonal-transversal` (inconsistente con
      su propio design.md, que ya usaba `Reto.Tests`)

## 2. Verificación de que nada más referencia el nombre viejo

- [x] 2.1 Grep de `CargaMasiva.Tests` en todo el repo — quedan 2 grupos:
      los propios artifacts de este change (esperado, documentan el rename) y
      `openspec/changes/carga-masiva-microservicios/specs/frontend-cliente-react.md:137`
- [x] 2.2 La referencia en `frontend-cliente-react.md` pertenece a otro change
      ya declarado fuera de scope en el proposal de este change — no se toca
      acá; queda como hallazgo aparte para quien retome ese spec

## 3. Validación (prueba determinística)

- [x] 3.1 `dotnet build Reto.slnx` — 14 proyectos, 0 errores
- [x] 3.2 `dotnet test tests/Reto.Tests/Reto.Tests.csproj` — 99/99 en verde
      (100 anteriores menos el placeholder eliminado)
- [x] 3.3 Namespace `Reto.Tests` confirmado en los 15 archivos de test
      restantes (grep sin resultados de `CargaMasiva.Tests` dentro de
      `tests/Reto.Tests/`)
