# Estrategia de integración de `AGENTS.md` v0.1

## Decisión de Gate 1

El `AGENTS.md` raíz vigente es la fuente de contexto operacional del producto: contiene
arquitectura, comandos comprobables, nueve contenedores, límites DIP, pruebas,
seguridad y reglas para preservar cambios del usuario. **No se reemplaza ni se reduce
durante Gate 1.**

La propuesta de secure delivery es un complemento de gobernanza, no una nueva
constitución aislada. Mientras `TBD-AGENT-01` y Gate 1.1 no estén aceptados, no se
activa una instrucción adicional ni se habilita un agente con autoridad material.

## Estructura de integración propuesta

```text
AGENTS.md (vigente)
├── propósito, inicio seguro, arquitectura y comandos reales
├── límites DIP, pruebas y seguridad del producto
└── sección futura: "Secure delivery" (enlace y reglas imperativas breves)

docs/agents/
├── AGENTS-integration-strategy.md   ← esta estrategia
├── AGENTS.md.proposed.md            ← texto candidato, no reemplazo
├── README.md                         ← modelo de gobernanza de agentes
└── skills/                           ← procedimientos propuestos, no activados

docs/architecture/ y docs/github/
└── riesgos, controles, adaptadores, fuentes y evidencia detallados
```

## Reglas de composición cuando haya autorización

1. Conservar íntegro el contexto de producto y los comandos comprobables del
   `AGENTS.md` vigente.
2. Incorporar solo reglas de delivery que sean breves, operables y compatibles con
   el mandato humano aprobado; los detalles permanecen enlazados en `docs/`.
3. Prohibir explícitamente prompts ocultos, evasión de revisión, secretos y que un
   agente sea autor, verificador, aprobador, integrador y desplegador único.
4. Si se necesitan instrucciones específicas para rutas de delivery, añadirlas como
   complemento de alcance más estrecho; no duplicar ni contradecir las invariantes
   del producto.
5. Probar que un agente sigue el flujo de revisión independiente antes de conceder
   acceso a cambios de control, ramas confiables o despliegues.

## Criterio de aceptación futuro

Una propuesta de modificación al `AGENTS.md` raíz debe mostrar un diff pequeño,
referenciar el control aplicable, preservar los límites de arquitectura y recibir
revisión humana. El cambio no se considera activación de CI/CD ni autorización de
despliegue.
