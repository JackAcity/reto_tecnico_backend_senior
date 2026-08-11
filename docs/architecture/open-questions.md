# Preguntas abiertas y decisiones humanas requeridas

| ID | Pregunta / decisión | Dueño propuesto | Impacto si queda abierta | Bloquea |
| --- | --- | --- | --- | --- |
| TBD-RISK-01 | Aceptar la [rúbrica v0.1](risk-classification.v0.1.md), nombrar autoridad humana y confirmar umbrales bajo/medio/alto. | Riesgo + ingeniería | Gates inconsistentes o excesivos. | Implementación de políticas de promoción. |
| TBD-SSDF-01 | Evaluar aplicabilidad de tareas SSDF v1.1 al producto y obligaciones regulatorias. | Seguridad/compliance | Afirmaciones de cobertura sin base. | Mapeo de requisitos finales. |
| TBD-GH-01 | Confirmar repositorio objetivo, plan/visibilidad, administradores, permissions y capacidades del [mapa GitHub](../github/platform-capability-map.md). | Dueño del repositorio | No se pueden comprobar controles de plataforma ni trasladar supuestos de público a privado. | Adaptador GitHub. |
| TBD-ID-01 | Elegir proveedor/runtime y modelo de identidad OIDC con claims permitidos. | Plataforma + seguridad | Riesgo de secretos cloud de larga duración o despliegue sin destino. | CD/deployment. |
| TBD-EVID-01 | Aprobar retención, acceso, privacidad y destrucción de evidencia. | Seguridad/legal/operación | Auditoría incompleta o retención inadecuada. | Política de evidencia. |
| TBD-OPS-01 | Elegir observabilidad, SLOs, métricas DORA y estrategia de rollback. | Operación/producto | No puede verificarse recuperación ni entrega. | Controles de runtime. |
| TBD-AGENT-01 | Aprobar usos permitidos de agentes, datos prohibidos, revisores y trazabilidad. | Seguridad + ingeniería | Agentes pueden recibir o producir cambios sin gobernanza. | Habilitación de agentes. |
| TBD-EXC-01 | Definir quién acepta excepciones, duración máxima y escalamiento. | Riesgo | Las excepciones se vuelven permanentes y opacas. | Activación de gates. |
| TBD-SCM-01 | Decidir mainline/trunk, rol de `develop`, cadencia de integración y vida máxima de ramas. | Ingeniería + producto | No se puede afirmar alineación con evidencia DORA ni diseñar reglas proporcionadas. | CTL-001, CTL-002 y CTL-003. |
| TBD-DELIVERY-01 | Determinar si algún servicio de alto riesgo justifica progressive delivery y qué runtime/telemetría lo soporta. | Operación + dueño de riesgo | Canary/blue-green podría ser teatro o faltar mitigación de blast radius. | CTL-014. |
| TBD-REUSABLE-01 | Decidir si los workflows reutilizables serán locales, organizacionales o de terceros, y su política de acceso/versionado. | Plataforma + seguridad | Se pueden propagar secretos o referencias mutables a escala. | CTL-015. |
| TBD-LICENSE-01 | Elegir y publicar una licencia antes de promover el repositorio como referencia reutilizable o material de workshop. | Autor/dueño del repositorio | El repositorio público no tiene permiso explícito de reutilización. | Promoción pública reutilizable; no bloquea Gate 1 técnico. |

## Decisión de parada

**ENGINEERING DECISION:** mientras las preguntas bloqueantes estén abiertas, solo se permite investigación, diseño, evaluación simulada y cambios documentales. No se implementan workflows ni se declara un nivel de cumplimiento.
