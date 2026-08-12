# Estándar GitHub activo: CI

El adaptador GitHub está activo en `main`. El contrato de cada ejecución conserva
commit/ref exacto, definición de workflow, versiones de toolchain, inputs,
resultados, duración, runner y enlaces a artefactos de evidencia.

Los workflows versionados separan responsabilidades:

- [verify.yml](../../.github/workflows/verify.yml): política de workflow, secretos,
  Trivy, backend, frontend e integración aislada con Docker Compose.
- [dependency-review.yml](../../.github/workflows/dependency-review.yml): auditoría
  NuGet y npm de cada PR.

Cada check falla explícitamente cuando su condición no se cumple. Trivy bloquea
HIGH/CRITICAL; sus coincidencias de secretos no se publican. El SARIF descargable
contiene únicamente resultados de vulnerabilidades y configuración. Los reportes se
conservan como evidencia conforme a la retención configurada en GitHub.

La validación no despliega. Cualquier `Package` o `Deploy` futuro debe usar un
workflow separado, secretos externos, entorno protegido, aprobación humana,
verificación posterior y rollback explícito.
