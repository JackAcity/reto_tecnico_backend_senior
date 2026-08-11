# Gate 1 — Architecture & Evidence Review

- Estado: **ready for human review; not approved for implementation**
- Fecha: 2026-08-11
- Baseline revisado: `7fbfa0a` más mejoras documentales de Gate 1 aún no publicadas
- Alcance: modelo de conocimiento, controles, amenazas, evidencia, evals y gobernanza de agentes

## Resultado de la revisión estructural

| Criterio | Resultado | Evidencia |
| --- | --- | --- |
| Skeleton requerido | Pass | 36 rutas requeridas presentes en `docs/` y `evals/`. |
| Registro de fuentes | Pass con revisión humana pendiente | [source register](../sources/source-register.md) diferencia final, draft, investigación y capacidad. |
| Catálogo de controles | Pass | 15 controles; cada uno contiene los 17 campos obligatorios. |
| Trazabilidad de fuentes | Pass | Cada `source_basis` del catálogo referencia un `source_id` registrado. |
| Modelo de amenazas | Pass con riesgo residual abierto | Activos, actores, límites, puntos de entrada y 18 rutas de ataque. |
| Modelo de evidencia | Pass | Sobre de evidencia y cadena de trazabilidad definidos. |
| Eval plan | Pass | 23 casos con contrato completo: vulnerable, compliant y exception. |
| AGENTS y skills | Pass como propuestas | [AGENTS propuesto](../agents/AGENTS.md.proposed.md) y tres responsabilidades separadas. |
| Implementación CI/CD | No iniciada deliberadamente | No hay workflows, rulesets, environments, secrets ni despliegues creados en este gate. |

## Hallazgos corregidos durante Gate 1

1. **G1-01 — Evidencia DORA específica:** se añadieron fuentes separadas para CI, TBD y pequeños lotes. DORA se conserva como investigación; sus tiempos/cadencias no se convirtieron en gate universal.
2. **G1-02 — Progressive delivery:** CTL-014 exige decidir la estrategia por riesgo y permite `not-applicable` justificado. No presupone canary, blue-green ni soporte de runtime inexistente.
3. **G1-03 — Reusable workflows:** CTL-015 controla contratos de inputs/secrets, referencias inmutables y `secrets: inherit`; incluye casos vulnerables y conformes.
4. **G1-04 — Modelo de amenazas:** se hicieron explícitos los puntos de entrada, incluido prompt, salida de herramienta y diff de agente.

## Conflictos y límites que deben permanecer visibles

- SSDF 1.1 es final; SSDF 1.2 es un borrador inicial. No se admite declarar v1.2 como baseline normativa.
- DORA es evidencia de capacidad, no certificación ni requisito de cumplimiento. Su descripción de TBD/CI crea una decisión de adopción, no un mandato ciego.
- Una attestation/SBOM/provenance demuestra procedencia o composición conforme a política; no prueba ausencia de vulnerabilidades.
- GitHub ofrece mecanismos técnicos; no sustituye segregación organizacional, decisiones de riesgo ni controles del proveedor cloud.

## Decisiones requeridas para aceptar Gate 1

| Decisión | Opciones que deben evaluarse | Consecuencia |
| --- | --- | --- |
| `TBD-SCM-01` | Trunk/mainline, `develop` como integración, o ramas de release. | Define qué significa CI y la política de integración. |
| `TBD-RISK-01` | Criterios y autoridad de perfiles bajo/medio/alto. | Define qué controles se activan y quién los acepta. |
| `TBD-GH-01` | Repositorio, plan GitHub, administradores y gobernanza real. | Determina capacidades configurables y pruebas de API/configuración. |
| `TBD-ID-01` | Runtime/proveedor, OIDC y roles por environment. | Bloquea deployment y secretos cloud. |
| `TBD-EVID-01` / `TBD-OPS-01` | Retención, auditoría, métricas, SLOs y rollback. | Bloquea la evidencia operativa y CTL-011/014. |
| `TBD-AGENT-01` | Datos permitidos, trazabilidad y aprobaciones de agentes. | Bloquea habilitar agentes con privilegios de entrega. |
| `TBD-REUSABLE-01` | Alcance/política de reusable workflows. | Bloquea centralizar workflows. |

## Recomendación de decisión

**Recomendar: aceptar Gate 1 solo como diseño v0.1**, tras responder los TBD anteriores. Aún no recomendar implementar CI/CD. Una vez aceptado, el siguiente mandato debe pedir un mínimo vertical de cinco o seis controles, cada uno con evaluación adversarial y evidencia, no una colección masiva de workflows.

## Paquete para revisor externo

Entregar estos enlaces, junto con esta pregunta:

1. [Source register](../sources/source-register.md)
2. [Control catalog](../architecture/control-catalog.md) y [YAML](../architecture/control-catalog.v0.1.yaml)
3. [GitHub capability map](../github/platform-capability-map.md)
4. [Threat model](../architecture/threat-model.md)
5. [Eval plan](../../evals/README.md)
6. [AGENTS proposal](../agents/AGENTS.md.proposed.md)
7. [ADR-0001](../adr/ADR-0001-platform-neutral-control-model.md)

> ¿El modelo separa correctamente fuente, requisito, control, adaptador, verificación y evidencia? ¿Qué decisión humana falta antes de autorizar un mínimo vertical de controles GitHub? No evaluar YAML de CI ni permitir implementación en esta revisión.
