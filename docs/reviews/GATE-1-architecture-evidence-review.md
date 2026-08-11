# Gate 1.1 — Architecture & Evidence Review

- Estado: **SUBSTANTIVE PASS — diseño listo para aceptación humana final**
- Autorización de implementación: **denegada**
- Fecha: 2026-08-11
- Sujeto de revisión independiente: `eb55aea4cffe543ec4b521457079ce8ddc34ef88`
- Baseline padre, previo a la remediación: `824f765374cb7904eda830d70c7f0185771c008f`
- Alcance: modelo de conocimiento, controles, amenazas, evidencia, evals,
  gobernanza de agentes y precondiciones del adaptador GitHub

## Decisión vigente

La dirección arquitectónica está aceptada sustancialmente: la cadena
`fuente → riesgo → requisito → control → adaptador → verificación → evidencia`
permanece independiente de GitHub. El SHA anterior es el objeto inmutable sometido a
revisión independiente; estas aclaraciones finales no cambian su dirección
arquitectónica. La aceptación humana final debe identificar explícitamente el commit
o tag inmutable que acepte. No se han creado ni modificado workflows, rulesets,
environments, secretos, deployments, identidad cloud ni configuración administrativa.

## Trazabilidad de remediación independiente

| ID | Hallazgo | Remediación y evidencia | Estado |
| --- | --- | --- | --- |
| G1-R01 | CTL-002 confundía CI con reproducibilidad bit a bit y citaba DORA CD. | CTL-002 ahora se llama **CI y build canónico/trazable**, cita `SRC-DORA-CI` y modela reproducibilidad bit a bit como extensión guiada por riesgo. | Resuelto documentalmente. |
| G1-R02 | El mapa GitHub omitía seis controles. | [Capability map](../github/platform-capability-map.md) contiene CTL-001 a CTL-015 y declara `partial`, `external` o `no-native-capability`. | Resuelto documentalmente. |
| G1-R03 | CTL-012/013 no tenían caminos directos y faltaba matriz. | La [matriz](../../evals/coverage-matrix.v0.1.md) enlaza los 15 controles con `fail` y `pass`; se añadieron EVAL-V-017/018 y EVAL-C-006..015. | Resuelto como cobertura de diseño; no ejecutado. |
| G1-R04 | El esquema no visibilizaba coste/fricción. | Los 15 controles YAML incluyen `cost_and_friction` con cuatro dimensiones cualitativas; el [modelo](../architecture/control-model.md) lo hace obligatorio. | Resuelto documentalmente. |
| G1-R05 | La propuesta podía reemplazar el AGENTS raíz y perder invariantes. | [Estrategia de integración](../agents/AGENTS-integration-strategy.md) conserva el `AGENTS.md` actual y trata delivery como sección aditiva propuesta. | Resuelto documentalmente; no activado. |
| G1-R06 | LOW/MEDIUM/HIGH no tenía función ni autoridad explícita. | [Rúbrica](../architecture/risk-classification.v0.1.md) define factores, puntaje, gatillos, roles y evidencia. `TBD-RISK-01` requiere aceptación/nombres humanos. | Resuelto como propuesta operable; decisión humana pendiente. |
| G1-R07 | `develop` era default sin decisión de mainline. | [ADR-0002](../adr/ADR-0002-trusted-mainline-strategy.md) evalúa A/B/C y recomienda `main` como mainline; no cambia ramas. | Decisión humana pendiente. |
| G1-R08 | Faltaban plan, visibilidad, permisos y dependencias externas del adaptador. | El mapa añade `plan_prerequisite`, `visibility_prerequisite`, `admin_permission` y `external_dependency` por control. | Resuelto documentalmente; se comprobará por repositorio objetivo. |
| G1-R09 | Dependabot, dependency review, secret scanning y push protection no tenían fuente primaria propia. | [Source register](../sources/source-register.md) incorpora cuatro fuentes GitHub oficiales y el catálogo las usa en CTL-004/005. | Resuelto documentalmente. |
| G1-R10 | Faltaban amenazas de MCP/contexto y plano administrativo. | Threat model añade TM-19/TM-20 y trust boundaries TB-09/TB-10. | Resuelto documentalmente. |
| G1-R11 | Reutilización pública no tenía licencia explícita. | `TBD-LICENSE-01` deja la decisión visible; no bloquea el Gate técnico. | Pendiente del dueño del repositorio. |
| G1-R12 | El Gate no identificaba con precisión el objeto publicado revisado. | El encabezado registra el SHA de revisión `eb55aea...` y su baseline padre `824f765...`. | Resuelto documentalmente. |
| G1-R13 | Riesgo global y aplicabilidad de CTL-004 podían contradecirse. | La rúbrica, catálogo y modelo separan perfil de riesgo, trigger de aplicabilidad y profundidad de ejecución. | Resuelto documentalmente. |
| G1-R14 | Todos los TBD parecían bloquear por igual el primer vertical. | La decisión diferencia los bloqueantes de Gate 2A de los verticales de runtime/despliegue posteriores. | Resuelto documentalmente; decisiones humanas pendientes. |
| G1-R15 | El fixture secreto neutral podía interpretarse como detector GitHub garantizado. | La suite declara que el adaptador ejecutable debe usar un fixture sintético compatible con el detector evaluado. | Resuelto documentalmente. |

## Resultado verificable de Gate 1.1

| Criterio | Resultado | Evidencia |
| --- | --- | --- |
| Modelo conceptual independiente de plataforma | Pass | [ADR-0001](../adr/ADR-0001-platform-neutral-control-model.md), [control model](../architecture/control-model.md). |
| Catálogo de controles | Pass de diseño | 15 controles, incluyendo coste/fricción cualitativo; [YAML](../architecture/control-catalog.v0.1.yaml). |
| CTL-002 semánticamente acotado | Pass de diseño | CI/trazabilidad separadas de reproducibilidad estricta. |
| Riesgo, aplicabilidad y profundidad | Pass de diseño | La clasificación no apaga controles por superficie; ajusta su profundidad. |
| Fuentes de plataforma | Pass de registro | Fuentes GitHub primarias para dependencias y secretos; vigencia se revalida antes de adaptar. |
| Mapa GitHub | Pass de cobertura documental | 15/15 controles con prerrequisitos y límites explícitos. |
| Modelo de amenazas | Pass de diseño | 20 rutas, incluyendo agentes, MCP/contexto y plano administrativo. |
| Evaluaciones | Pass de cobertura de diseño | 35 casos: 18 vulnerable, 15 compliant, 2 exception; no ejecutados. |
| AGENTS y skills | Pass como propuestas | El `AGENTS.md` del producto se preserva; no hay activación de delivery. |
| Implementación CI/CD | **No iniciada deliberadamente** | No hay cambios de plataforma dentro de este gate. |

## Decisiones humanas por vertical

### Gate 2A — Repository Trust Vertical

Los siguientes TBD bloquean únicamente el primer vertical propuesto: CTL-001,
CTL-003, CTL-005, CTL-006, CTL-007 (solo `GITHUB_TOKEN`) y CTL-012.

| Decisión | Estado requerido para Gate 2A |
| --- | --- |
| `TBD-RISK-01` | Aceptar la rúbrica y nombrar autoridad humana de riesgo. |
| `TBD-SCM-01` | Aceptar ADR-0002 y decidir mainline/default antes de cambiar ramas. |
| `TBD-GH-01` | Declarar este repositorio público como primer adaptador y registrar sus capacidades efectivas. |
| `TBD-AGENT-01` | Limitar al agente a candidato/checks; prohibir aprobación de riesgo, administración, autoaprobación, merge confiable y despliegue. |
| `TBD-EVID-01` | Aprobar retención/acceso mínimos para la evidencia de los seis controles, no toda la política corporativa. |
| `TBD-EXC-01` | Exigir dueño humano, motivo, compensación y expiración; ninguna excepción automática. |

### Verticales posteriores

| Decisión | Vertical que bloquea, no Gate 2A |
| --- | --- |
| `TBD-ID-01` | OIDC cloud y despliegues (parte cloud de CTL-007, CTL-010). |
| `TBD-OPS-01` | Observabilidad, SLO, rollback y recuperación (CTL-011, CTL-014). |
| `TBD-DELIVERY-01` | Progressive delivery proporcional (CTL-014). |
| `TBD-REUSABLE-01` | Centralización/reuso de workflows (CTL-015). |

## Recomendación para la revisión humana Gate 1.1

1. Verificar la traza G1-R01..G1-R15 contra los documentos enlazados y reconocer el
   SHA sujeto de revisión.
2. Aceptar, rechazar o ajustar `TBD-RISK-01`, `ADR-0002` y los seis límites de Gate
   2A enumerados arriba.
3. Confirmar los prerrequisitos reales de GitHub sin trasladar supuestos de este
   repositorio público a uno privado de cliente.
4. Si las decisiones de Gate 2A reciben dueño y aprobación explícita, autorizar un
   **primer vertical mínimo**, no un paquete masivo de CI/CD: CTL-001, CTL-003,
   CTL-005, CTL-006, CTL-007 y CTL-012, cada uno con camino adversarial ejecutado y
   evidencia conservada.

Hasta esa autorización, este repositorio solo permite investigación, diseño,
evaluaciones conceptuales y documentación. No permite implementación de controles de
plataforma.
