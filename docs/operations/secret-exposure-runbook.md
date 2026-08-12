# Runbook: exposición de secreto

## Activación

Este flujo se aplica ante una alerta de GitHub, un hallazgo del detector local o una
sospecha razonable. Trate cualquier valor plausible como comprometido aunque luego
resulte ser un falso positivo.

1. Detener la propagación: bloquear el PR o revertir el cambio sin copiar el valor a
   comentarios, tickets, logs ni mensajes.
2. Revocar o rotar el secreto en su emisor. No basta con borrar el valor del archivo.
3. Retirar el valor del historial y de artefactos accesibles conforme al procedimiento
   de respuesta aplicable; evaluar cachés, logs y despliegues que pudieron consumirlo.
4. Registrar identificador de alerta, commit, archivos afectados, dueño, hora de
   revocación y resultado de validación. Nunca registrar el secreto.
5. Confirmar que el detector ya no encuentra el patrón y que el sistema usa la
   credencial reemplazada.
6. Si el secreto llegó a una rama confiable o a producción, escalar al dueño de
   seguridad antes de cerrar el incidente.

Los fixtures de Gate 2A se generan en un directorio temporal y usan un patrón
sintético; no se versiona ni se distribuye una credencial real. Un secreto real no
admite excepción automática.
