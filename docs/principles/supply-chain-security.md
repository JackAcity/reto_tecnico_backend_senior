# Principio: seguridad de la cadena de suministro

**FACT:** SLSA v1.2 modela niveles y provenance; GitHub advierte que una attestation se debe verificar para aportar valor. **ENGINEERING DECISION:** los artefactos relevantes se identifican por digest y se comprueban contra una política de consumo.

SBOM, provenance y attestation son tipos de evidencia complementarios. No prueban por sí mismos que un artefacto sea seguro; CTL-004, CTL-006 y CTL-009 se evalúan conjuntamente.
