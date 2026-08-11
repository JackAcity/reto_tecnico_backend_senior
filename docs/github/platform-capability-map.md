# Mapa de capacidades GitHub v0.1

Este mapa traduce controles aprobados a candidatos GitHub. **PLATFORM CAPABILITY** no significa que esté configurado en este repositorio ni que el control sea universal.

| Control | Capacidad GitHub documentada | Fuente | Límite / verificación futura |
| --- | --- | --- | --- |
| CTL-001 | Rulesets para ramas/tags, reglas acumulables y visibles a lectores. | [SRC-GH-RULESETS](../sources/source-register.md) | Requiere permisos administrativos y prueba de merge/push denegado. |
| CTL-003 | Required status checks como regla de merge. | [Available rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets) | Un nombre de check no demuestra que mida correctamente el control. |
| CTL-006 | Referencia de seguridad Actions y política empresarial de SHA completo. | [SRC-GH-ACTIONS-SEC](../sources/source-register.md) | Pinning no sustituye revisión de comportamiento ni aislamiento. |
| CTL-007 | `permissions` por workflow/job y OIDC con `id-token: write`. | [SRC-GH-OIDC](../sources/source-register.md) | El proveedor cloud debe validar claims y scope. |
| CTL-008 | Diferencia de aislamiento para self-hosted runners en environments. | [SRC-GH-ENV](../sources/source-register.md) | La plataforma no diseña por sí sola la red, imagen ni ciclo de vida del runner. |
| CTL-009 | Artifact attestations, provenance, SBOM y verificación. | [SRC-GH-ATTEST](../sources/source-register.md) | Debe existir política de consumo; generar attestation no basta. |
| CTL-010 | Environments, reviewers, no self-review, restricciones de rama/tag y secretos por environment. | [SRC-GH-ENV](../sources/source-register.md) | La segregación de deberes fuera de GitHub y el proveedor siguen siendo necesarios. |
| CTL-011 | Historial de deployments/environments. | [Deployments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments) | No sustituye métricas, alertas ni rollback en runtime. |

## Observación del contexto actual

**FACT:** la cuenta conectada en esta sesión solo permitió lectura del repositorio y no permisos administrativos de GitHub. Por eso ningún ruleset, secret, environment, deployment ni configuración de Actions se ha creado como parte de v0.1. Esta limitación es coherente con la fase de diseño.

## Capacidad no equivale a adecuación

Antes de adoptar una capacidad se debe registrar: control asociado, riesgo, perfil, coste, datos/identidad requeridos, verificación, evidencia, dueño y plan de reversión. El catálogo evita convertir una característica popular en un mandato sin justificación.
