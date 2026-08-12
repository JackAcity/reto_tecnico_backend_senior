# Gate 2B: gobernanza de dependencias

- Estado: **candidato implementado; activación de Dependabot Security Updates pendiente de merge**
- Fecha: 2026-08-12
- Baseline inmutable: \`391e2d91fec892414d401ccd990a60064814d280\`
- Adaptador objetivo: repositorio público \`JackAcity/reto_tecnico_backend_senior\`
- Control: CTL-004 — Gobernanza de dependencias

## Objetivo y límite

Gate 2B hace identificables y revisables las dependencias ejecutables que cambian.
Cubre NuGet, npm, GitHub Actions y Docker Compose. No afirma una SBOM, provenance,
política de licencias o corrección automática de vulnerabilidades: esas decisiones
pertenecen a verticales posteriores o requieren una política humana explícita.

El check \`dependency-review\` solo analiza PRs que modifican un manifest, lockfile o
la configuración de Dependabot. Rechaza dependencias **nuevas** con severidad alta o
crítica. No interpreta la ausencia de un finding como ausencia de riesgo.

## Evidencia de baseline

El 2026-08-12 se ejecutó:

\`\`\`powershell
dotnet restore Reto.slnx --disable-build-servers
dotnet list Reto.slnx package --vulnerable --include-transitive
Set-Location frontend
npm ci
npm audit --omit=dev --audit-level=high
\`\`\`

Resultado: no se reportaron vulnerabilidades conocidas para los 18 proyectos NuGet ni
para las dependencias npm de producción. El restore conservó la advertencia existente
\`NU1510\` sobre \`System.Text.Encoding.CodePages\`; no es una vulnerabilidad y no se
modifica en este gate.

## Implementación

- [dependabot.yml](../../.github/dependabot.yml) programa revisiones semanales con
  cobertura explícita de los directorios NuGet, \`frontend\`, workflows y Compose.
- [dependency-review.yml](../../.github/workflows/dependency-review.yml) usa la
  acción oficial fijada a SHA completo, token de solo lectura y sin comentarios de
  bot. Esto preserva CTL-006 y CTL-007.
- La configuración agrupa actualizaciones por dependencia en NuGet para evitar un PR
  por cada proyecto cuando una biblioteca compartida cambia.

La política de licencia no se infiere. Antes de configurarla se debe decidir qué
licencias son aceptables, cómo se manejan dependencias transitorias y quién puede
aprobar una excepción.

## Activación posterior al merge

Después de fusionar el PR, el dueño humano habilita Dependabot Security Updates desde
**Settings → Code security and analysis → Dependabot security updates**. El endpoint
de solo lectura debe mostrar \`enabled\`. No se activa antes de que el archivo
versionado de configuración esté en \`main\`.

## Evidencia y excepciones

Por una actualización o alerta conservar: PR, SHA, manifest/lockfile afectado,
resultado de \`dependency-review\`, alerta o advisory, decisión de actualización y
resultado de CI. Una excepción requiere paquete/CVE, compensación, dueño y fecha de
vencimiento. Dependabot no se auto-fusiona; las actualizaciones pasan por el mismo
PR protegido.
