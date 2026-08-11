# Estándar GitHub propuesto: cadena de suministro

**FACT — SRC-GH-ATTEST:** GitHub explica que las attestations establecen procedencia y deben verificarse; no garantizan que un artefacto sea seguro. **FACT — SRC-SLSA-1.2:** SLSA define modelos y requisitos crecientes para el track de build.

Una implementación candidata debe producir artefactos por digest, SBOM y provenance/attestation cuando el perfil lo requiera. Un verificador separado debe validar emisor, subject, commit, workflow/política y destino antes de promover o ejecutar. La etiqueta de un registro puede ayudar a localizar, pero no sustituye el digest.

La declaración de un nivel SLSA está prohibida hasta que la evaluación contra la versión de especificación aplicable y la evidencia real haya sido aprobada.
