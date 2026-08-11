# Modelo de evidencia v0.1

## Principio

**ENGINEERING DECISION:** un control no se considera demostrado por existir en un archivo de configuración. Debe producir evidencia verificable, identificada y retenida de acuerdo con su perfil.

## Sobre de evidencia mínimo

| Campo | Propósito |
| --- | --- |
| `evidence_id` | Identificador único y estable. |
| `control_id` | Control que se pretende demostrar. |
| `subject` | Commit, artefacto por digest, deployment o configuración observada. |
| `producer` | Sistema/rol que emitió la evidencia e identidad usada. |
| `timestamp` | Momento de producción y zona/precisión. |
| `input_refs` | Versiones de fuente, política, workflow y dependencias relevantes. |
| `verification_method` | Procedimiento determinista o revisión humana documentada. |
| `result` | Pass, fail, not-applicable, exception o inconclusive. |
| `integrity_ref` | Hash, firma, URL inmutable o referencia a snapshot. |
| `retention` | Período, ubicación y dueño de borrado. |
| `approval_ref` | Aprobación requerida y rol, cuando aplique. |
| `exception_ref` | Riesgo aceptado, vencimiento y responsable, cuando aplique. |

## Cadena de trazabilidad objetivo

`solicitud/issue → commit → PR/revisión → ejecución CI → resultados → artefacto digest → SBOM/provenance → decisión de promoción → deployment → observabilidad/rollback`.

Cada flecha es una relación que el auditor debe poder consultar. Una etiqueta mutable, un log sin commit o una aprobación sin artefacto no completa la cadena.

## Retención y acceso

**ASSUMPTION:** el proveedor elegido permitirá exportar resultados y eventos necesarios. **TBD-EVID-01:** retención legal, ubicación, cifrado, acceso de auditoría y política de borrado. Los artefactos de prueba no deben contener datos reales ni secretos.
