# Preguntas abiertas y decisiones humanas requeridas

| ID | Pregunta / decisión | Dueño propuesto | Impacto si queda abierta | Bloquea |
| --- | --- | --- | --- | --- |
| TBD-RISK-01 | Definir criterios y autoridad para perfiles bajo/medio/alto. | Riesgo + ingeniería | Gates inconsistentes o excesivos. | Implementación de políticas de promoción. |
| TBD-SSDF-01 | Evaluar aplicabilidad de tareas SSDF v1.1 al producto y obligaciones regulatorias. | Seguridad/compliance | Afirmaciones de cobertura sin base. | Mapeo de requisitos finales. |
| TBD-GH-01 | Confirmar repositorio objetivo, plan GitHub, administradores y capacidad de configurar rulesets/environments. | Dueño del repositorio | No se pueden comprobar controles de plataforma. | Adaptador GitHub. |
| TBD-ID-01 | Elegir proveedor/runtime y modelo de identidad OIDC con claims permitidos. | Plataforma + seguridad | Riesgo de secretos cloud de larga duración o despliegue sin destino. | CD/deployment. |
| TBD-EVID-01 | Aprobar retención, acceso, privacidad y destrucción de evidencia. | Seguridad/legal/operación | Auditoría incompleta o retención inadecuada. | Política de evidencia. |
| TBD-OPS-01 | Elegir observabilidad, SLOs, métricas DORA y estrategia de rollback. | Operación/producto | No puede verificarse recuperación ni entrega. | Controles de runtime. |
| TBD-AGENT-01 | Aprobar usos permitidos de agentes, datos prohibidos, revisores y trazabilidad. | Seguridad + ingeniería | Agentes pueden recibir o producir cambios sin gobernanza. | Habilitación de agentes. |
| TBD-EXC-01 | Definir quién acepta excepciones, duración máxima y escalamiento. | Riesgo | Las excepciones se vuelven permanentes y opacas. | Activación de gates. |

## Decisión de parada

**ENGINEERING DECISION:** mientras las preguntas bloqueantes estén abiertas, solo se permite investigación, diseño, evaluación simulada y cambios documentales. No se implementan workflows ni se declara un nivel de cumplimiento.
