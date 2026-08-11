# ADR-0001: separar el modelo de controles del adaptador GitHub

- Estado: aceptado para fase de diseño
- Fecha: 2026-08-11
- Decisores: autor del repositorio y responsables humanos de seguridad/plataforma pendientes de designación

## Contexto

El repositorio será una referencia de entrega segura para clientes empresariales. GitHub es la primera plataforma candidata, pero requisitos escritos como “usar Rulesets” o “usar GitHub Actions” no son portables ni explican el riesgo que pretenden mitigar. Además, GitHub aún no está autorizado/configurado administrativamente para este trabajo.

## Decisión

Se adopta un núcleo de control independiente de plataforma, con la cadena:

`evidencia fuente → riesgo → requisito → control → adaptador → verificación → evidencia producida`.

GitHub, GitLab y Azure DevOps serán adaptadores candidatos. Toda implementación debe apuntar a un `control_id`, declarar permisos, límites de confianza, método de verificación y evidencia; una capacidad del proveedor no crea por sí sola un requisito.

## Consecuencias

### Positivas

- La arquitectura puede añadir otro SCM/CI sin reescribir sus riesgos, controles o evals.
- Las decisiones son auditables y se evita activar herramientas sin objetivo comprobable.
- La evidencia no queda reducida a un archivo YAML o a un dashboard de proveedor.

### Costes

- Diseñar el catálogo y las evaluaciones antecede a implementar workflows.
- Las capacidades de GitHub deben volver a verificarse al momento de adaptar.
- Se necesita dueños humanos para riesgo, excepciones, identidad y operación.

## Alternativas descartadas

1. **Empezar por workflows GitHub genéricos.** Rechazada: mezcla requisito, mecanismo y evidencia; no soporta portabilidad ni evaluación suficiente.
2. **Usar solo una lista de prácticas DevSecOps.** Rechazada: no tiene cadena de riesgo, método de prueba ni tratamiento de excepciones.
3. **Permitir que un agente configure y certifique los controles.** Rechazada: viola separación de deberes y convierte al generador en única fuente de confianza.

## Criterio de revisión

Revisar antes de añadir el primer adaptador no GitHub, al aprobar infraestructura/runtime, o si una fuente final cambia materialmente el modelo de control.
