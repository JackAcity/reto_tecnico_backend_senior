# ADR-0002: estrategia de mainline confiable

- Estado: **propuesta — requiere aceptación humana antes de cambiar ramas**
- Fecha: 2026-08-11
- Decisores requeridos: ingeniería, dueño del repositorio y producto
- Evidencia observada: al 2026-08-11, `git ls-remote --symref jackacity HEAD`
  devuelve `develop` como rama predeterminada. Esta observación no autoriza un
  cambio administrativo.

## Contexto

CTL-001, CTL-002 y CTL-003 requieren una rama confiable definida. DORA asocia CI y
trunk-based development con integración frecuente y ramas de vida corta, pero es
evidencia de capacidad, no una obligación de adoptar una topología idéntica para todo
repositorio. La topología elegida debe permitir checks rápidos, historial auditable y
promoción proporcional al riesgo.

## Opciones evaluadas

| Opción | Ventajas | Costes / riesgos | Veredicto |
| --- | --- | --- | --- |
| A. `main` como mainline confiable y default; ramas cortas → PR → CI → `main`. | Modelo simple, claro para una referencia nueva, reduce una rama de integración permanente y alinea el vocabulario de CI con una mainline única. | Requiere migrar el default y la documentación, proteger `main` y acordar proceso de release. | **Recomendada, pendiente de aprobación.** |
| B. `develop` como mainline confiable/default; ramas cortas → PR → CI → `develop`; `main` solo releases. | Respeta el default observado y puede servir si existe una cadencia formal de release. | Riesgo de que `main` se atrase, se dupliquen gates y se use `develop` como rama larga sin disciplina. | Válida solo con reglas explícitas de promoción y release. |
| C. Otra topología justificada (por ejemplo, release branches temporales). | Puede responder a soporte de versiones o requisitos regulatorios reales. | Mayor complejidad, más combinaciones de gates y riesgo de merges tardíos. | Requiere justificación de producto/operación y fecha de revisión. |

## Decisión propuesta

Adoptar **A**: `main` será la mainline confiable y rama predeterminada. Los cambios
se integrarán desde ramas de corta duración por PR con CI rápido y controles
proporcionales. Las release branches solo se crearán por necesidad explícita y se
retirarán tras su objetivo.

`develop` no se elimina ni cambia en esta fase. Antes de activar la decisión se debe
aprobar la propuesta, migrar el default con autoridad administrativa, definir el
tratamiento de PRs abiertos, proteger la rama y actualizar la guía de contribución.

## Consecuencias

- CTL-001 puede definir una única rama confiable y CTL-002/003 pueden medir CI contra
  la misma integración.
- No se declara cumplimiento DORA: la decisión solo adopta una topología coherente
  con la evidencia registrada en `SRC-DORA-CI` y `SRC-DORA-TBD`.
- Si se elige B o C, el catálogo se actualizará para nombrar de forma consistente la
  rama confiable, sin reescribir los controles independientes de plataforma.

## Criterio de aceptación

La decisión queda aceptada únicamente con una aprobación humana registrada en
`TBD-SCM-01`. Gate 1.1 debe verificar que el ADR no se confunda con un cambio real de
configuración de ramas.
