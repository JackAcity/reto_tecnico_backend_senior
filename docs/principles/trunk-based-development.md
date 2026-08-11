# Principio: mainline y lotes pequeños

**RESEARCH EVIDENCE — SRC-DORA-TBD y SRC-DORA-SMALL-BATCHES:** DORA presenta TBD y pequeños lotes como prácticas necesarias de CI en su modelo; no los convierte en una norma universal de cumplimiento.

**ENGINEERING DECISION:** una rama de integración confiable debe mantenerse desplegable mediante cambios pequeños, revisables y frecuentes. La topología concreta debe preservar integración frecuente y evitar ramas largas, no copiar mecánicamente el nombre de una estrategia.

La variante concreta —trunk directo, `develop` o release branches— depende de riesgo, cadencia y compatibilidad. **TBD-SCM-01:** aprobar si `develop` es una rama de integración de vida corta o si el repositorio adoptará trunk como mainline, antes de activar CTL-001. No se afirmará que el flujo actual es TBD hasta medir su cadencia y duración de ramas.
