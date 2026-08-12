# Preguntas abiertas y decisiones humanas requeridas

| ID | Pregunta / decisión | Dueño propuesto | Impacto si queda abierta | Bloquea |
| --- | --- | --- | --- | --- |
| TBD-RISK-01 | Aceptar la [rúbrica v0.1](risk-classification.v0.1.md), nombrar autoridad humana y confirmar umbrales bajo/medio/alto. | Riesgo + ingeniería | Profundidad de controles inconsistente o aceptación de riesgo sin dueño. | Gate 2A y políticas de promoción. |
| TBD-SSDF-01 | Evaluar aplicabilidad de tareas SSDF v1.1 al producto y obligaciones regulatorias. | Seguridad/compliance | Afirmaciones de cobertura sin base. | Mapeo de requisitos finales. |
| TBD-GH-01 | Confirmar este repositorio como primer objetivo, plan/visibilidad, administradores, permissions y capacidades del [mapa GitHub](../github/platform-capability-map.md). | Dueño del repositorio | No se pueden comprobar controles de plataforma ni trasladar supuestos de público a privado. | Gate 2A para el adaptador GitHub. |
| TBD-ID-01 | Elegir proveedor/runtime y modelo de identidad OIDC con claims permitidos. | Plataforma + seguridad | Riesgo de secretos cloud de larga duración o despliegue sin destino. | Vertical de deployment y OIDC cloud; no bloquea prueba de `GITHUB_TOKEN` mínimo en Gate 2A. |
| TBD-EVID-01 | Aprobar retención/acceso mínimos de evidencia para Gate 2A y, después, privacidad/destrucción corporativa completa. | Seguridad/legal/operación | Auditoría incompleta o retención inadecuada. | Mínimo Gate 2A; política completa para verticales operativos. |
| TBD-OPS-01 | Elegir observabilidad, SLOs, métricas DORA y estrategia de rollback. | Operación/producto | No puede verificarse recuperación ni entrega. | Controles de runtime CTL-011/014; no bloquea Gate 2A. |
| TBD-AGENT-01 | Aprobar usos permitidos de agentes, datos prohibidos, revisores y trazabilidad. | Seguridad + ingeniería | Agentes pueden recibir o producir cambios sin gobernanza. | Gate 2A para CTL-012. |
| TBD-EXC-01 | Definir aceptación humana, duración máxima, compensación y escalamiento; prohibir excepciones automáticas. | Riesgo | Las excepciones se vuelven permanentes y opacas. | Gate 2A y activación de gates. |
| TBD-SCM-01 | Definir cadencia de integración y vida máxima de ramas. `main` ya es la mainline protegida y `develop` la rama de integración alineada por merge. | Ingeniería + producto | Ramas largas pueden volver a divergir pese a la topología actual. | CTL-002 y CTL-003. |
| TBD-DELIVERY-01 | Determinar si algún servicio de alto riesgo justifica progressive delivery y qué runtime/telemetría lo soporta. | Operación + dueño de riesgo | Canary/blue-green podría ser teatro o faltar mitigación de blast radius. | CTL-014. |
| TBD-REUSABLE-01 | Decidir si los workflows reutilizables serán locales, organizacionales o de terceros, y su política de acceso/versionado. | Plataforma + seguridad | Se pueden propagar secretos o referencias mutables a escala. | CTL-015. |
| TBD-LICENSE-01 | Elegir y publicar una licencia antes de promover el repositorio como referencia reutilizable o material de workshop. | Autor/dueño del repositorio | El repositorio público no tiene permiso explícito de reutilización. | Promoción pública reutilizable; no bloquea Gate 1 técnico. |

## Decisión de parada

**ENGINEERING DECISION:** Gate 2A y Gate 2B están activos en `main`; sus controles no dependen de los TBD de runtime o despliegue. Mientras un bloqueante de un vertical futuro siga abierto, solo se permite investigación, diseño, evaluación simulada y cambios documentales en ese vertical. No se declara cumplimiento de cloud, despliegue, OIDC, observabilidad o licencias hasta que sus decisiones y evidencia existan.
