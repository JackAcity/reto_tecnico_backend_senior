# Gate 2B: gobernanza de dependencias

- Estado: **activo y requerido en toda PR hacia `main`**
- Activación verificada: 2026-08-12
- Baseline inicial: `391e2d91fec892414d401ccd990a60064814d280`
- Adaptador: repositorio público `JackAcity/reto_tecnico_backend_senior`
- Control: CTL-004 — Gobernanza de dependencias

## Objetivo y límite

Gate 2B hace identificables y revisables las dependencias ejecutables. Cubre NuGet,
npm, GitHub Actions y Docker Compose. No afirma SBOM, provenance, política de
licencias ni corrección automática de vulnerabilidades: esas decisiones requieren
política humana o controles posteriores.

`dependency-audit` se ejecuta en **toda** PR hacia `main`, además de poder iniciarse
manualmente. Restaura el grafo NuGet y ejecuta `npm audit`; falla ante una
vulnerabilidad conocida de severidad alta o crítica. Ejecutarlo en toda PR evita que
un check requerido quede ausente solo porque el diff no cambió un manifest.

## Evidencia de baseline

El 2026-08-12 se ejecutó:

```powershell
dotnet restore Reto.slnx --disable-build-servers
dotnet list Reto.slnx package --vulnerable --include-transitive
Set-Location frontend
npm ci
npm audit --omit=dev --audit-level=high
```

No se reportaron vulnerabilidades conocidas para los 18 proyectos NuGet ni para las
dependencias npm de producción. La advertencia `NU1510` sobre
`System.Text.Encoding.CodePages` no es una vulnerabilidad y no se modifica en este
gate.

## Implementación activa

- [dependabot.yml](../../.github/dependabot.yml) programa revisiones semanales de
  NuGet, `frontend`, workflows y Compose.
- [dependency-review.yml](../../.github/workflows/dependency-review.yml) ejecuta
  auditoría NuGet y npm con token de solo lectura, acciones fijadas a SHA y checkout
  sin credenciales persistentes.
- Dependabot alerts y Dependabot security updates están habilitados; Dependabot no
  se auto-fusiona.
- Los cambios de Compose, imágenes y acciones pasan por el mismo PR protegido. La
  actualización PostgreSQL 18 quedó separada como una migración dedicada porque
  requiere layout de volumen, `pg_upgrade`, backup, restauración y rollback probados.

La política de licencias no se infiere. Antes de configurarla se debe decidir qué
licencias son aceptables, cómo se manejan dependencias transitorias y quién aprueba
una excepción.

## Evidencia y excepciones

Por actualización o alerta conservar PR, SHA, manifest/lockfile afectado, resultado
de `dependency-audit`, alerta/advisory, decisión y resultado de CI. Una excepción
requiere paquete/CVE, compensación, dueño y fecha de vencimiento.
