# Secure Software Delivery Reference Architecture

Esta documentación define la fase de diseño de una arquitectura de entrega de software segura, portable y verificable. No afirma que el repositorio cumpla un estándar ni activa controles de CI/CD: esas decisiones requieren la aprobación humana indicada en [Preguntas abiertas](architecture/open-questions.md).

## Navegación

- [Registro de fuentes v0.1](sources/source-register.md): evidencia primaria, estado y límites de cada fuente.
- [Modelo conceptual y terminología](architecture/control-model.md): separa el control independiente de plataforma de su posible adaptador GitHub.
- [Catálogo de controles v0.1](architecture/control-catalog.md) y su [representación legible por máquina](architecture/control-catalog.v0.1.yaml).
- [Modelo de amenazas v0.1](architecture/threat-model.md) y [límites de confianza](architecture/trust-boundaries.md).
- [Modelo de evidencia v0.1](architecture/evidence-model.md).
- [Mapa de capacidades GitHub](github/platform-capability-map.md).
- [Matriz de evaluación](../evals/README.md): los escenarios que un auditor deberá detectar antes de acreditar un control.
- [Gobernanza de agentes](agents/README.md): un agente genera candidatos; no es una fuente autónoma de confianza.
- [ADR-0001](adr/ADR-0001-platform-neutral-control-model.md): decisión de separar control conceptual de adaptación de plataforma.
- [Gate 1 — Architecture & Evidence Review](reviews/GATE-1-architecture-evidence-review.md): paquete de aprobación humana antes de cualquier workflow.

## Convenciones de afirmación

Cada afirmación relevante se etiqueta de forma explícita:

| Etiqueta | Significado |
| --- | --- |
| **FACT** | Hecho verificable en una fuente o en evidencia del repositorio. |
| **STANDARD REQUIREMENT** | Requisito textual de una norma final aplicable. |
| **PLATFORM CAPABILITY** | Función específica documentada de una plataforma. |
| **RESEARCH EVIDENCE** | Hallazgo empírico; no crea obligación por sí mismo. |
| **ENGINEERING DECISION** | Decisión propuesta o aprobada para este repositorio. |
| **ASSUMPTION** | Dato provisional que exige confirmación. |
| **HYPOTHESIS** | Idea que se debe evaluar mediante evidencia. |
| **EXCEPTION** | Desviación aceptada, con dueño, riesgo y vencimiento. |

La fecha de acceso de esta primera versión es **2026-08-11**. Las páginas web vivas se deben volver a verificar antes de una implementación o auditoría.
