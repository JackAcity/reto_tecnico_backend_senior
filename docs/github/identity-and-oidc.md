# Estándar GitHub propuesto: identidad y OIDC

**PLATFORM CAPABILITY — SRC-GH-OIDC:** GitHub permite que un job solicite un token OIDC con `id-token: write`; el proveedor debe confiar en GitHub y aplicar condiciones para que repositorios no confiables no obtengan acceso.

## Contrato conceptual

- Una identidad humana aprueba según política; no comparte la identidad de build o deployment.
- Una identidad de build no despliega por defecto.
- Una identidad de deployment obtiene credenciales efímeras solo para el environment, repositorio, ref, audiencia y rol autorizados.
- Los secretos de larga duración se eliminan de la ruta de deployment cuando OIDC puede sustituirlos.

**TBD-ID-01:** proveedor, claims inmutables aplicables, audiencia, roles, duración de token, rotación y prueba de denegación. No se configurará una confianza cloud sin esta decisión.
