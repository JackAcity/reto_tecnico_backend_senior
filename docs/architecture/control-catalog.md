# Catálogo de controles v0.1

La fuente de verdad legible por máquina es [control-catalog.v0.1.yaml](control-catalog.v0.1.yaml). Esta vista facilita la revisión humana. Todos los controles tienen estado **propuesto**: ninguno está implementado ni verificado en esta fase.

| ID | Control | Riesgo principal | Perfil | Adaptador GitHub candidato | Verificación requerida |
| --- | --- | --- | --- | --- | --- |
| CTL-001 | Integración de mainline protegida | Integración no autorizada | Todos | Rulesets / protección de ramas | Intento de merge/push sin requisitos. |
| CTL-002 | Build reproducible y CI | Artefacto no repetible | Todos | Workflow de CI versionado | Rebuild del mismo commit y comparación de resultados definidos. |
| CTL-003 | Calidad y testing automatizado | Regresión no detectada | Todos | Checks requeridos | Prueba deliberadamente fallida bloquea la promoción. |
| CTL-004 | Gobernanza de dependencias | Componente vulnerable o no revisado | Medio/alto | Dependabot, review, SCA | PR con dependencia vulnerable/nueva se detecta. |
| CTL-005 | Protección de secretos | Credencial expuesta | Todos | Secret scanning / push protection | Fixture secreto dispara detector sin guardar un secreto real. |
| CTL-006 | Seguridad de workflows y acciones | Ejecución no confiable / acción mutable | Todos | Política Actions, SHA pin, revisión de workflows | Evaluación detecta trigger y referencia inseguros. |
| CTL-007 | Privilegio mínimo e identidad no humana | Token con alcance excesivo | Todos; estricto alto | `permissions`, GitHub App/OIDC por rol | Inspección de permisos y prueba de acción denegada. |
| CTL-008 | Aislamiento del runner | Contaminación o fuga entre jobs | Medio/alto | Hosted/ephemeral runners | Evidencia de tipo de runner y negativa a PR no confiable. |
| CTL-009 | Integridad del artefacto | Sustitución / build sin trazabilidad | Medio/alto | Digest, SBOM, attestation | Verificación independiente de subject, commit y policy. |
| CTL-010 | Autorización de despliegue | Bypass o escalación de environment | Alto | Environments, revisores, reglas de deployment | Intento de autoaprobación/despliegue no autorizado falla. |
| CTL-011 | Recuperación y observabilidad | Release fallido sin diagnóstico/rollback | Medio/alto | Integración runtime, deployments, logs | Drill de rollback y consulta de evidencia. |
| CTL-012 | Gobernanza de agentes | Agente se auto-confía o fuga datos | Todos | AGENTS/skills + branch governance | Caso de agente no puede ser único verificador/aprobador. |
| CTL-013 | Cambios de datos e IaC | Cambio irrevertible o infraestructura insegura | Medio/alto | Checks y environments futuros | Caso de migración/IaC sin plan no promueve. |

## Lectura de la cadena de control

Ejemplo conceptual de CTL-010: el riesgo es un despliegue de producción sin autorización; el requisito protege un destino de alto impacto; el control exige una decisión de promoción separada; un environment de GitHub es solo un candidato; la verificación intenta un self-approval; la evidencia es configuración, identidad, decisión y deployment log. Si cualquiera falta, el control falla aunque el workflow termine verde.
