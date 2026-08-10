## Context

`tests/CargaMasiva.Tests` es hoy el único proyecto de test del repo. Nació
cuando solo existía CargaMasiva, pero ya prueba los 5 servicios (Auth, Control,
Notificaciones, Gateway, CargaMasiva) y BuildingBlocks compartido. El change
`arquitectura-hexagonal-transversal` va a agregar archivos de test nuevos a
este mismo proyecto (guardia de arquitectura, `Result<T>`, puerto de datos) —
este change se resuelve primero para que esos archivos nazcan ya en el
nombre/namespace final.

## Goals / Non-Goals

**Goals:**
- Proyecto de test con nombre que refleja su alcance real (todo el repo).
- Cero test placeholder sin valor.
- Cero cambio de comportamiento: mismas aserciones, mismos casos.

**Non-Goals:**
- No reorganiza los tests en carpetas por servicio (eso no lo pidió la
  auditoría; se puede evaluar aparte si el proyecto de test crece más).
- No agrega tests nuevos — eso es scope de `arquitectura-hexagonal-transversal`.
- No toca código de producción (`src/`).

## Decisions

- **Rename vía `git mv` + edición de namespace, no borrar y recrear**: preserva
  historial de cada archivo (`git log --follow` sigue funcionando). Alternativa
  descartada: borrar `CargaMasiva.Tests` y crear `Reto.Tests` desde cero —
  pierde el historial de cada test individual sin ninguna ventaja a cambio.
- **Namespace `Reto.Tests` en todos los `.cs` existentes**: reemplazo mecánico
  de `namespace CargaMasiva.Tests;` → `namespace Reto.Tests;`, sin tocar el
  cuerpo de ninguna clase.
- **`UnitTest1.cs` se elimina, no se renombra**: no prueba nada, no hay
  contenido que preservar.

## Risks / Trade-offs

- [Riesgo] Otro `.csproj` o el `.sln` referencian `CargaMasiva.Tests` por rutas
  relativas o `ProjectReference` → build roto tras el rename.
  Mitigación: `dotnet build` sobre la solución completa como parte de la
  verificación (tasks.md), no solo `dotnet test` del proyecto renombrado.
- [Riesgo] Algún test usa `nameof(CargaMasiva.Tests...)` o reflection sobre el
  nombre de assembly para algo funcional (no solo diagnóstico).
  Mitigación: grep de `CargaMasiva.Tests` en `src/` y `tests/` antes de dar el
  rename por cerrado — si aparece, se documenta como hallazgo aparte, no se
  improvisa un fix fuera de este scope.

## Migration Plan

1. `git mv tests/CargaMasiva.Tests tests/Reto.Tests`.
2. `git mv tests/Reto.Tests/CargaMasiva.Tests.csproj tests/Reto.Tests/Reto.Tests.csproj`.
3. Reemplazar `namespace CargaMasiva.Tests;` → `namespace Reto.Tests;` en todos
   los `.cs` de `tests/Reto.Tests/`.
4. Eliminar `tests/Reto.Tests/UnitTest1.cs`.
5. Actualizar la referencia del proyecto en `reto_tecnico_backend_senior.sln`
   (ruta y nombre).
6. `dotnet build` de la solución completa, luego `dotnet test tests/Reto.Tests`.

Sin rollback especial: es un rename atómico en un solo commit: revertir el
commit alcanza.
