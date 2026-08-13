# Gate 2A: confianza del repositorio

- Estado: **activo y verificado en `main`**
- Activación verificada: 2026-08-12
- Baseline inicial: `e51d82b3f6f453004896eb22ec898a6ffd211506`
- Adaptador: repositorio público `JackAcity/reto_tecnico_backend_senior`
- Autoridad operativa: dueño humano del repositorio y revisor humano independiente

## Propósito y límite

Gate 2A establece el vertical de confianza del repositorio: PR protegido, permisos
mínimos, workflows verificables y evidencia repetible. No afirma cobertura de cloud,
OIDC, despliegues, SLO, rollback, provenance ni SBOM; esos controles requieren sus
propios verticales y decisiones humanas.

## Controles activos

`main` exige siete checks actualizados antes de fusionar una PR:

1. `workflow-policy`: rechaza triggers privilegiados, permisos de escritura y
   acciones sin pin inmutable; incluye fixture adversarial.
2. `secret-policy`: comprueba detección sintética sin imprimir coincidencias.
3. `trivy`: bloquea vulnerabilidades o misconfiguraciones HIGH/CRITICAL, analiza
   secretos sin publicar sus coincidencias y conserva SARIF de vulnerabilidades e
   infraestructura como evidencia.
4. `backend`: restore y build Release reproducible de la solución.
5. `frontend`: instalación por lockfile, lint, test y build.
6. `integration`: levanta Compose desde el mismo SHA, ejecuta la suite y elimina el
   stack efímero.
7. `dependency-audit`: audita NuGet y npm en toda PR; pertenece a Gate 2B, pero es
   requisito de la protección de rama.

Los workflows usan `contents: read`, acciones fijadas a SHA y checkout sin
credenciales persistentes. La configuración y sus scripts están en
[`.github/workflows`](../../.github/workflows) y [`scripts/ci`](../../scripts/ci).

## Protección de `main` verificada

- `main` es la rama por defecto.
- Las reglas se aplican a administradores, prohíben force push y borrado.
- Se exige una aprobación humana, distinta del último autor de push, y resolución de
  conversaciones.
- GitHub Actions usa permisos por defecto de solo lectura y no puede aprobar PRs.
- Dependabot alerts y Dependabot security updates están habilitados. Secret Scanning
  y Push Protection deben reevaluarse si cambian visibilidad o plan de GitHub.

## Evidencia y excepciones

Conservar por cambio URL de PR, SHA, URL/ID de run, conclusión de checks y
artefactos de seguridad cuando existan. Una excepción requiere motivo, alcance,
compensación, dueño y vencimiento. Un secreto real no se exceptúa; su respuesta está
especificada en el [runbook de exposición](../operations/secret-exposure-runbook.md).

## Límites honestos

Un pipeline verde no sustituye revisión humana, pruebas de capacidad ni garantías de
producción. La primera ejecución local anterior al job aislado tuvo fallos por usar
contenedores de otro checkout; esa evidencia no se presenta como una suite verde. El
job `integration` actual elimina esa desalineación construyendo y retirando su propio
stack.
