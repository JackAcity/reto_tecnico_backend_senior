# NIST SSDF

## Estado de la fuente

**FACT — SRC-NIST-SSDF-1.1:** NIST SP 800-218, SSDF v1.1, se publicó como versión final en febrero de 2022. Es la línea base final usada por este diseño.

**FACT — SRC-NIST-SSDF-1.2-IPD:** la revisión 1 / SSDF v1.2 es un *Initial Public Draft* de diciembre de 2025. No se usa como requisito final.

## Interpretación de arquitectura

**ENGINEERING DECISION:** se modelan los controles con el ciclo `riesgo → requisito → control → implementación → verificación → evidencia`. Este modelo puede trazar prácticas SSDF sin acoplar la arquitectura a un CI, un proveedor cloud o GitHub.

SSDF es un marco de recomendaciones que se integra a SDLCs existentes. Esta arquitectura no afirma certificación SSDF: la aplicabilidad, la evidencia y las excepciones deben ser aprobadas por el dueño de riesgo.

## Uso en el catálogo

- Preparar la organización orienta controles de políticas, propiedad, formación y criterios de excepción.
- Proteger el software orienta revisión, análisis y gestión de dependencias.
- Producir software bien protegido orienta build reproducible, integridad de artefacto, SBOM y provenance.
- Responder a vulnerabilidades orienta divulgación, seguimiento y recuperación.

Las correspondencias exactas por tarea SSDF quedan pendientes de una evaluación de aplicabilidad por producto (**TBD-SSDF-01**); no se infieren únicamente por el nombre de un control.
