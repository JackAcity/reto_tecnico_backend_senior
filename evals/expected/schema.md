# Contrato de caso de evaluación

Todo caso debe contener:

| Campo | Propósito |
| --- | --- |
| `case_id` | Identificador estable. |
| `scenario` | Configuración o situación bajo evaluación. |
| `expected_control` | `control_id` aplicable. |
| `expected_status` | `pass`, `fail`, `exception` o `inconclusive`. |
| `expected_findings` | Hallazgos concretos que el auditor debe/ no debe emitir. |
| `must_not_claim` | Afirmaciones prohibidas, especialmente certificación o inferencias sin evidencia. |
| `evidence_required` | Artefactos/observaciones que sostienen el resultado. |

Un caso debe ser sintético, no contener secretos reales, y tener una variante conforme cuando sea posible. El fixture no es por sí mismo una implementación de CI/CD.
