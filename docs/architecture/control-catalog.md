# Catálogo de controles v0.1

La fuente de verdad legible por máquina es [control-catalog.v0.1.yaml](control-catalog.v0.1.yaml). Esta vista facilita la revisión humana. Todos los controles tienen estado **propuesto**: ninguno está implementado ni verificado en esta fase.

| ID | Control | Riesgo principal | Perfil | Adaptador GitHub candidato | Verificación requerida |
| --- | --- | --- | --- | --- | --- |
| CTL-001 | Integración de mainline protegida | Integración no autorizada | Todos | Rulesets / protección de ramas | Intento de merge/push sin requisitos. |
| CTL-002 | CI y build canónico/trazable | Integración rota o build sin trazabilidad | Cambios ejecutables | Workflow de CI versionado | Build aplicable enlaza commit, inputs, toolchain y resultados. |
| CTL-003 | Calidad y testing automatizado | Regresión no detectada | Todos | Checks requeridos | Prueba deliberadamente fallida bloquea la promoción. |
| CTL-004 | Gobernanza de dependencias | Componente vulnerable o no revisado | Cambio de dependency/lockfile/toolchain | Dependabot, review, SCA | PR con dependencia vulnerable/nueva se detecta. |
| CTL-005 | Protección de secretos | Credencial expuesta | Todos | Secret scanning / push protection | Fixture secreto dispara detector sin guardar un secreto real. |
| CTL-006 | Seguridad de workflows y acciones | Ejecución no confiable / acción mutable | Todos | Política Actions, SHA pin, revisión de workflows | Evaluación detecta trigger y referencia inseguros. |
| CTL-007 | Privilegio mínimo e identidad no humana | Token con alcance excesivo | Todos; estricto alto | `permissions`, GitHub App/OIDC por rol | Inspección de permisos y prueba de acción denegada. |
| CTL-008 | Aislamiento del runner | Contaminación o fuga entre jobs | Todo job; estricto con input no confiable/self-hosted | Hosted/ephemeral runners | Evidencia de tipo de runner y negativa a PR no confiable. |
| CTL-009 | Integridad del artefacto | Sustitución / build sin trazabilidad | Artefacto distribuible o promovible | Digest, SBOM, attestation | Verificación independiente de subject, commit y policy. |
| CTL-010 | Autorización de despliegue | Bypass o escalación de environment | Despliegue a environment protegido | Environments, revisores, reglas de deployment | Intento de autoaprobación/despliegue no autorizado falla. |
| CTL-011 | Recuperación y observabilidad | Release fallido sin diagnóstico/rollback | Cambio de runtime o persistencia | Integración runtime, deployments, logs | Drill de rollback y consulta de evidencia. |
| CTL-012 | Gobernanza de agentes | Agente se auto-confía o fuga datos | Todos | AGENTS/skills + branch governance | Caso de agente no puede ser único verificador/aprobador. |
| CTL-013 | Cambios de datos e IaC | Cambio irrevertible o infraestructura insegura | DB, secretos, red, IAM, runtime o IaC | Checks y environments futuros | Caso de migración/IaC sin plan no promueve. |
| CTL-014 | Estrategia de promoción | Blast radius desproporcionado | Alto cuando aplica | Environment + proveedor externo | Caso canary/rollback y caso not-applicable. |
| CTL-015 | Reusable workflows gobernados | Secreto/permiso heredado o ref mutable | Cuando se reutilicen | `workflow_call` con contrato explícito | Fixture de SHA/secret explícito frente a `inherit` injustificado. |

## Lectura de la cadena de control

Ejemplo conceptual de CTL-010: el riesgo es un despliegue de producción sin autorización; el requisito protege un destino de alto impacto; el control exige una decisión de promoción separada; un environment de GitHub es solo un candidato; la verificación intenta un self-approval; la evidencia es configuración, identidad, decisión y deployment log. Si cualquiera falta, el control falla aunque el workflow termine verde.

## Coste y fricción

Cada control contiene en el YAML cuatro estimaciones cualitativas obligatorias:
`implementation`, `recurring`, `developer_experience` e
`infrastructure_platform_dependency`. No son un presupuesto ni una aprobación de
compra: hacen visible el intercambio entre riesgo mitigado, experiencia de
desarrollo y dependencia de plataforma antes de adoptar el adaptador.

CTL-002 no exige reproducibilidad *bit-for-bit*. Esta capacidad puede añadirse como
extensión separada cuando el perfil de riesgo, el tipo de artefacto y el coste la
justifiquen. El control base exige CI y un build canónico que se pueda identificar y
auditar.

El perfil de riesgo no es un interruptor global de controles. `applicability`
identifica la superficie que activa un control; `execution_depth` define cuánto se
endurece según riesgo. Por eso CTL-004 puede aplicar a una actualización de
dependencia de riesgo bajo, sin imponer la misma profundidad de evidencia que a una
actualización de alto riesgo.
