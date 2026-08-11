# Mapa de capacidades GitHub v0.1

Este mapa traduce **los 15 controles** a candidatos GitHub. `partial`, `external`
y `no-native-capability` son resultados válidos: una característica del proveedor no
puede ocultar que una parte del control vive en el runtime, la organización o un
proveedor de identidad.

| Control | Estado del adaptador | Capacidad / fuente | `plan_prerequisite` | `visibility_prerequisite` | `admin_permission` | `external_dependency` | Límite de verificación |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CTL-001 | partial | Rulesets + CODEOWNERS; `SRC-GH-RULESETS`, `SRC-GH-CODEOWNERS`. | Confirmar disponibilidad del plan al configurar. | No inferir paridad entre repo público y privado. | Administrar reglas/protección de rama. | No. | Probar push/merge rechazado; CODEOWNERS no reemplaza el gate. |
| CTL-002 | partial | GitHub Actions ejecuta el build versionado. | Actions habilitado y cuota/runner suficientes. | Según política del repositorio. | Escribir/revisar workflows; exigir check requiere gobierno de rama. | Runner y almacenamiento de resultados. | Actions no demuestra reproducibilidad bit a bit ni decide la topología de ramas. |
| CTL-003 | partial | Checks de Actions + reglas de merge. | Actions y reglas/checks disponibles. | Según política del repositorio. | Administrar workflow y regla de integración. | Ejecutor de pruebas. | Un check requerido debe fallar de verdad ante un fixture. |
| CTL-004 | partial | Dependency review + Dependabot; `SRC-GH-DEPENDENCY-REVIEW`, `SRC-GH-DEPENDABOT-ALERTS`. | Dependency review: público, o privado organizacional con GitHub Code Security habilitado. | Público disponible; privado depende del producto habilitado. | Administrar seguridad/dependency graph y reglas. | Ecosistema soportado, grafo y datos de advisories. | No cubre toda dependencia dinámica ni sustituye triage. |
| CTL-005 | partial | Secret scanning + push protection; `SRC-GH-SECRET-SCANNING`, `SRC-GH-PUSH-PROTECTION`. | Público: scanning automático; privado organizacional: GitHub Secret Protection en Team/Enterprise Cloud según documentación vigente. | Las capacidades varían entre público, privado e interno. | Administrar configuración de seguridad/bypass. | Gestor de secretos y runbook de revocación. | Patrones, tamaño de push y tiempos de escaneo tienen límites. |
| CTL-006 | partial | Seguridad de Actions y pinning; `SRC-GH-ACTIONS-SEC`. | Políticas organizacionales pueden requerir plan/administración superior; confirmar. | Según política de Actions del repositorio/organización. | Administrar Actions y revisión de workflows. | Inventario de acciones y, si aplica, proveedor de runners. | Pinning no sustituye revisión de comportamiento ni aislamiento. |
| CTL-007 | partial | `permissions`, GitHub App y OIDC; `SRC-GH-TOKEN`, `SRC-GH-OIDC`. | Actions/OIDC habilitados. | No asumir que una política pública aplica igual a privada. | Administrar App, secretos y políticas de repositorio. | Proveedor cloud/IdP que valide claims. | GitHub no valida por sí solo la trust policy del cloud. |
| CTL-008 | partial | Selección de hosted/self-hosted runners; `SRC-GH-ACTIONS-SEC`. | Capacidad y presupuesto de runner apropiados. | No aplica de forma universal. | Administrar runners/grupos/labels. | Imagen, red, secreto y ciclo de vida del runner. | GitHub no diseña el aislamiento de red ni la limpieza del runner. |
| CTL-009 | partial | Artifact attestations; `SRC-GH-ATTEST`. | Confirmar capacidad de attestation/retención para el plan elegido. | Confirmar política para tipo de repositorio/artefacto. | Administrar Actions/paquetes y política de consumo. | Registro y verificador de provenance/SBOM. | Generar attestation no obliga al runtime a verificarla. |
| CTL-010 | partial | Environments/protection rules; `SRC-GH-ENV`. | Required reviewers tienen restricciones de plan; validar antes de adoptar. | En Free/Pro/Team, la documentación condiciona required reviewers a repos públicos. | Administrar environments y reglas de despliegue. | Runtime, proveedor cloud e identidad. | La segregación organizacional y autorización cloud quedan fuera de GitHub. |
| CTL-011 | external | Historial de deployments puede enlazar evidencia; `SRC-GH-ENV`. | Actions/deployments disponibles; confirmar retención. | Según plan/política. | Administrar deployments y acceso a evidencia. | Observabilidad, backup, runtime y runbooks. | GitHub no mide SLOs ni ejecuta una recuperación válida por sí solo. |
| CTL-012 | no-native-capability | PR, CODEOWNERS y auditoría solo apoyan; `SRC-GH-CODEOWNERS`. | Ninguno para el requisito conceptual. | No aplica. | Gobierno de rama/identidades si se usan. | Plataforma de agentes, MCP/herramientas, revisores humanos. | GitHub no impone por sí solo separación de funciones de un agente. |
| CTL-013 | partial | Checks y environments pueden gatear; `SRC-GH-ENV`. | Depende de checks/environments disponibles. | No asumir paridad público/privado. | Administrar checks, environments y secretos. | DB, proveedor IaC, backup y validadores de plan. | GitHub no valida semántica de una migración o un rollback. |
| CTL-014 | external | Environment puede orquestar un gate; `SRC-GH-ENV`. | Confirmar gating disponible si se usa. | Depende de plan/visibilidad. | Administrar environment/flujo de release. | Runtime que soporte segmentación, señales y rollback. | GitHub no implementa canary, rolling o blue-green por sí solo. |
| CTL-015 | partial | `workflow_call` y contratos reutilizables; `SRC-GH-REUSABLE`. | Actions y política de acceso a workflows reutilizables. | Acceso entre repos/organización debe validarse. | Administrar Actions y protección de workflows. | Inventario de callers y política organizacional. | `secrets: inherit` y permisos efectivos requieren auditoría explícita. |

## Observación del contexto actual

**FACT:** no se ha creado ni modificado ningún ruleset, secret, environment,
deployment, workflow de Actions ni configuración administrativa durante Gate 1. La
capacidad disponible y los permisos efectivos se deben observar en el repositorio
objetivo al iniciar un adaptador; este documento no los infiere.

## Capacidad no equivale a adecuación

Antes de adoptar una capacidad se debe registrar: control asociado, riesgo, perfil, coste, datos/identidad requeridos, verificación, evidencia, dueño y plan de reversión. El catálogo evita convertir una característica popular en un mandato sin justificación.
