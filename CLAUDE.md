# Entrada para Claude Code

Lee [AGENTS.md](AGENTS.md) antes de ejecutar comandos o proponer cambios. Es el
contrato visible y agnóstico del repositorio: contiene el arranque seguro,
validaciones, límites DIP, reglas de operación y formato de entrega.

Ruta rápida:

1. `git status --short --branch`
2. Crear `.env` solo si no existe, a partir de `.env.example`.
3. `docker compose up -d --build --wait` y confirmar los servicios.
4. Leer [README.md](README.md) y la
   [guía visual](docs/explicacion/README.md).
5. Ejecutar las validaciones descritas en `AGENTS.md` antes de declarar un
   resultado.

No cambies `.claude/settings.local.json`: es configuración local ignorada por
Git. Para una prueba ética de razonamiento técnico, usa el ejercicio explícito
en [docs/evaluacion-agentes.md](docs/evaluacion-agentes.md).
