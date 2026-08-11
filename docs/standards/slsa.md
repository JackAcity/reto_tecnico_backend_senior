# SLSA

## Estado de la fuente

**FACT — SRC-SLSA-1.2:** SLSA v1.2 define tracks, niveles y formatos de attestation, incluido provenance. Es una especificación de seguridad de la cadena de suministro; no una certificación otorgada por este repositorio.

## Decisión de diseño

**ENGINEERING DECISION:** la arquitectura guarda la identidad inmutable de artefacto (digest), el commit, la definición del build, el ejecutor y el resultado de verificación como clases distintas de evidencia. Una attestation es evidencia de procedencia, no prueba de ausencia de vulnerabilidades.

## Límites

- **ASSUMPTION:** la futura plataforma podrá producir y verificar provenance compatible con el formato elegido. Debe probarse con una muestra real.
- **HYPOTHESIS:** un workflow reusable aislado podría elevar las garantías de build frente a workflows por repositorio. Requiere evaluación de amenazas y no se afirma como nivel SLSA logrado.
- **EXCEPTION:** ningún artefacto sin digest verificable será apto para promoción de alto riesgo; una excepción requerirá dueño, justificación y vencimiento.
