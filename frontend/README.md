# Cliente web — Carga masiva

Frontend opcional del reto (§2.1 del enunciado): las 4 pantallas exigidas
(login, subida de Excel, historial, detalle), consumiendo el Gateway igual que
Postman/curl, sin lógica de negocio propia. Vite + React 19 + TypeScript.

Spec completo (contrato consumido, decisiones, escenarios) en
[`../openspec/changes/carga-masiva-microservicios/specs/frontend-cliente-react.md`](../openspec/changes/carga-masiva-microservicios/specs/frontend-cliente-react.md).

## Levantar

Con el stack de Docker ya arriba (`docker compose up -d --wait` en la raíz del
repo):

```bash
cp .env.example .env    # VITE_API_URL=http://localhost:8080
npm install
npm run dev              # http://localhost:5173
```

## Probar

```bash
npm test    # vitest + testing-library, sin backend (Login.test.tsx)
npx tsc -b  # type-check
```
