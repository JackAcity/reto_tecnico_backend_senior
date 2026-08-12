# Runbook humano: verificar y mantener Gate 2A en GitHub

El dueño humano ejecuta este runbook al activar o revisar la protección de `main`.
Un agente puede preparar evidencia y cambios versionados; las decisiones de riesgo,
revisiones independientes y excepciones siguen siendo humanas.

## Estado esperado

- `main` es la rama por defecto.
- Una PR requiere aprobación humana, aprobación posterior al último push y
  conversaciones resueltas.
- Los checks requeridos son `workflow-policy`, `secret-policy`, `trivy`, `backend`,
  `frontend`, `integration` y `dependency-audit`.
- La regla aplica a administradores y bloquea force push y borrado.
- Actions usa permisos por defecto de solo lectura y no puede aprobar PRs.
- Dependabot alerts/security updates están habilitados; verificar Secret Scanning y
  Push Protection según las capacidades vigentes del repositorio.

## Verificación de solo lectura

```powershell
gh api repos/JackAcity/reto_tecnico_backend_senior --jq '{default_branch, visibility}'
gh api repos/JackAcity/reto_tecnico_backend_senior/branches/main/protection
gh api repos/JackAcity/reto_tecnico_backend_senior/actions/permissions/workflow
gh api repos/JackAcity/reto_tecnico_backend_senior/automated-security-fixes
```

Revise también la página de protección/ruleset de la interfaz de GitHub: los nombres
de checks pueden cambiar si se renombra un workflow. Cree una PR de prueba cuando
cambie una regla y confirme que una PR sin aprobación o con check ausente no se puede
fusionar.

## Bypass y contingencia

Un bypass administrativo no es rutina. Si una emergencia lo exige, registrar dueño,
incidente, cambio, compensación, vencimiento y revisión posterior. Restablecer la
regla antes de cerrar el incidente.
