# Runbook humano: activar Gate 2A en GitHub

Este documento lo ejecuta el dueño humano del repositorio después de revisar el PR
del candidato. Un agente puede preparar el PR y leer el resultado, pero no debe
cambiar estas configuraciones ni fusionar el cambio.

## Precondiciones

- El PR candidato tiene los cinco checks verdes.
- Existe al menos un revisor humano distinto del autor con acceso al repositorio.
- Se confirmó que el repositorio sigue siendo el adaptador público acordado.
- Se registró cualquier PR abierto cuyo destino deba cambiar antes de mover la rama
  por defecto.

## Configuración en la interfaz de GitHub

1. En **Settings → Branches**, cambiar la rama por defecto a \`main\`.
2. Crear un ruleset dirigido a \`main\` y habilitar:
   - pull request obligatorio;
   - al menos una aprobación; no permitir que el autor se autoapruebe;
   - resolución de conversaciones;
   - checks requeridos y actualizados: \`workflow-policy\`, \`secret-policy\`,
     \`backend\`, \`frontend\` e \`integration\`;
   - bloquear force push y borrado;
   - aplicar la regla también a administradores, salvo un bypass explícito,
     auditado y temporal para emergencia.
3. En **Settings → Actions → General**, seleccionar permisos por defecto de solo
   lectura y deshabilitar que GitHub Actions cree o apruebe pull requests.
4. En **Security → Code security and analysis**, confirmar Secret Scanning. Habilitar
   Push Protection si aparece disponible para esta visibilidad y plan.
5. Registrar los enlaces a la regla, configuración de Actions y controles de
   secretos como evidencia del cambio.

No agregue \`CODEOWNERS\` hasta conocer los identificadores reales de los revisores;
una regla con un usuario supuesto produciría un control aparente pero inoperante.

## Verificación de solo lectura

Después de configurar, el dueño puede capturar estas observaciones con GitHub CLI:

\`\`\`powershell
gh api repos/JackAcity/reto_tecnico_backend_senior --jq '{default_branch, visibility}'
gh api repos/JackAcity/reto_tecnico_backend_senior/rulesets
gh api repos/JackAcity/reto_tecnico_backend_senior/actions/permissions/workflow
\`\`\`

Además, crear un PR de prueba sin aprobación y confirmar que GitHub no permite
fusionarlo. El fallo de un check no se simula como éxito: se conserva el enlace al
run rojo y luego se revierte el cambio de prueba antes de continuar.

## Bypass y contingencia

El bypass no es un mecanismo rutinario. Si una emergencia requiere usarlo, registrar
dueño, incidente, cambio exacto, compensación, vencimiento y revisión posterior.
Revisar la regla tras la emergencia y restablecer su configuración antes de cerrar el
incidente.
