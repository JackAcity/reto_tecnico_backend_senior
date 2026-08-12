# Gate 2A: confianza del repositorio

- Estado: **candidato implementado; activación administrativa pendiente**
- Fecha: 2026-08-12
- Baseline inmutable: \`e51d82b3f6f453004896eb22ec898a6ffd211506\`
- Adaptador objetivo: repositorio público \`JackAcity/reto_tecnico_backend_senior\`
- Autoridad de activación: dueño humano del repositorio y revisor humano independiente

## Propósito y límite

Gate 2A crea el primer vertical verificable de confianza del repositorio. Su alcance
es deliberadamente pequeño: CTL-001, CTL-003, CTL-005, CTL-006, CTL-007 para el
token de GitHub y CTL-012. No incorpora cloud, OIDC, despliegues, environments,
SLO, rollback, provenance, SBOM ni workflows reutilizables.

La conversación del 2026-08-12 autoriza preparar este candidato tras Gate 1.1. No
autoriza a un agente a cambiar el default branch, administrar rulesets, configurar
secretos, aprobar riesgo, autoaprobar, fusionar ni desplegar. Esas operaciones
permanecen separadas por diseño.

## Decisiones aplicadas al candidato

| Tema | Decisión candidata | Efecto verificable |
| --- | --- | --- |
| Mainline | ADR-0002 opción A: \`main\` será la mainline confiable. | El workflow escucha PR y push a \`main\`; el dueño debe hacer efectiva la topología. |
| Token de Actions | Solo \`contents: read\`. | El auditor rechaza permisos de escritura y \`write-all\`. |
| Eventos | Sin \`pull_request_target\` ni \`workflow_run\`. | El fixture adversarial debe ser rechazado. |
| Acciones | Referencias externas fijadas a SHA completo. | El fixture con \`actions/checkout@v4\` debe ser rechazado. |
| Secretos | Detector local complementario, sin imprimir coincidencias. | Un token sintético efímero debe ser detectado. |
| Integración | Compose se levanta desde el mismo checkout del runner. | La suite no confunde contenedores locales de otro commit con el código evaluado. |

## Implementación entregada

El workflow [verify.yml](../../.github/workflows/verify.yml) ejecuta cinco checks
con nombres estables:

1. \`workflow-policy\`: política de eventos, permisos y pinning, con fixture negativo.
2. \`secret-policy\`: detector sintético y fixture negativo sin secreto real.
3. \`backend\`: restore y build Release de la solución.
4. \`frontend\`: instalación reproducible por lockfile, lint, test y build.
5. \`integration\`: configuración de desarrollo no secreta, Compose del checkout,
   pruebas contra ese stack y limpieza del runner efímero.

Los scripts están en [scripts/ci](../../scripts/ci). El detector local no sustituye
GitHub Secret Scanning ni Push Protection: ofrece evidencia repetible dentro del
repositorio, mientras el dueño confirma las capacidades de GitHub disponibles.

## Activación humana obligatoria

Tras la revisión independiente del PR, el dueño debe seguir el
[runbook de GitHub](../operations/github-gate2a-owner-runbook.md). Gate 2A no está
activo hasta que exista evidencia de:

- \`main\` como default y una regla que impida push directo, force push y borrado;
- PR obligatorio con una aprobación humana distinta del autor;
- los cinco checks requeridos y resolución de conversaciones;
- Actions con permiso por defecto de solo lectura y sin permitir que Actions cree o
  apruebe PRs;
- Secret Scanning confirmado y Push Protection habilitado cuando la capacidad esté
  disponible para esta visibilidad y plan.

Un único dueño sin revisor independiente no satisface la separación de funciones:
debe incorporar un revisor humano antes de exigir aprobación obligatoria.

## Evidencia mínima y excepciones

Conservar por cada cambio: URL de PR, SHA, URL e identificador del run, conclusión
de cada check, snapshot o export de la regla de rama, estado de permisos de Actions,
resultado del detector de secretos y, cuando exista, identificador de alerta.

No existen excepciones automáticas. Una excepción requiere dueño humano, motivo,
compensación, alcance y vencimiento; un secreto real nunca se exceptúa. La respuesta
a una alerta está en el [runbook de exposición](../operations/secret-exposure-runbook.md).

## Resultado local observado antes de activar

El 2026-08-12, build Release de la solución pasó. La suite integrada contra los
contenedores locales existentes obtuvo 122/126 pruebas correctas y 4 fallos de carga
con HTTP 400. Esos tests se conectan a \`localhost:8080\`, mientras los contenedores
existentes no fueron construidos desde este checkout aislado. El archivo de muestra
de este checkout sí tenía firma ZIP válida. No se declara una suite verde por ese
resultado; el job \`integration\` elimina precisamente esa desalineación al construir
Compose desde el SHA que valida.
