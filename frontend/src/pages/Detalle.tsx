import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { obtenerDetalle, descargarContenido, ApiError, type DetalleCarga } from '../api/client';
import { EstadoBadge } from '../components/EstadoBadge';

const ESTADOS_TERMINALES = new Set(['Notificado', 'Rechazada', 'Bloqueada', 'Fallida']);
const INTERVALO_POLLING_MS = 3000;

export function Detalle() {
  const { id } = useParams<{ id: string }>();
  const idCarga = Number(id);
  const [detalle, setDetalle] = useState<DetalleCarga | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [descargando, setDescargando] = useState(false);

  const cargar = useCallback(async () => {
    try {
      setDetalle(await obtenerDetalle(idCarga));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No se pudo cargar el detalle.');
    }
  }, [idCarga]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  useEffect(() => {
    if (!detalle || ESTADOS_TERMINALES.has(detalle.carga.estado)) return;
    const timerId = setInterval(cargar, INTERVALO_POLLING_MS);
    return () => clearInterval(timerId);
  }, [detalle, cargar]);

  async function descargar() {
    if (!detalle) return;
    setDescargando(true);
    try {
      await descargarContenido(idCarga, detalle.carga.nombreArchivo);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No se pudo descargar el archivo.');
    } finally {
      setDescargando(false);
    }
  }

  if (error) return <p role="alert" className="error">{error}</p>;
  if (!detalle) return <p>Cargando…</p>;

  const { carga, periodos, errores, totalErrores } = detalle;

  return (
    <div className="tarjeta">
      <div className="encabezado-tarjeta">
        <h2>Carga #{carga.idCarga} — {carga.nombreArchivo}</h2>
        <EstadoBadge estado={carga.estado} />
      </div>

      <dl className="resumen">
        <dt>Usuario</dt><dd>{carga.usuario}</dd>
        <dt>Registrada</dt><dd>{new Date(carga.fechaRegistro).toLocaleString()}</dd>
        <dt>Finalizada</dt><dd>{carga.fechaFin ? new Date(carga.fechaFin).toLocaleString() : '—'}</dd>
        <dt>Filas insertadas</dt><dd>{carga.filasInsertadas}</dd>
        <dt>Filas rechazadas</dt><dd>{carga.filasRechazadas}</dd>
      </dl>

      {detalle.mensajeError && <p role="alert" className="error">{detalle.mensajeError}</p>}

      <button type="button" onClick={descargar} disabled={descargando}>
        {descargando ? 'Descargando…' : 'Descargar Excel original'}
      </button>

      <h3>Periodos</h3>
      <table>
        <thead><tr><th>Periodo</th><th>Estado</th><th>Insertadas</th></tr></thead>
        <tbody>
          {periodos.map((p) => (
            <tr key={p.periodo}><td>{p.periodo}</td><td>{p.estado}</td><td>{p.filasInsertadas}</td></tr>
          ))}
          {periodos.length === 0 && <tr><td colSpan={3}>Sin periodos resueltos todavía.</td></tr>}
        </tbody>
      </table>

      <h3>Errores auditados ({totalErrores})</h3>
      <table>
        <thead>
          <tr><th>Fila</th><th>Periodo</th><th>Código</th><th>Columna</th><th>Motivo</th><th>Valor crudo</th></tr>
        </thead>
        <tbody>
          {errores.map((e, i) => (
            <tr key={i}>
              <td>{e.numeroFila}</td>
              <td>{e.periodo ?? '—'}</td>
              <td>{e.codigoProducto ?? '—'}</td>
              <td>{e.columna ?? '—'}</td>
              <td>{e.motivo}</td>
              <td>{e.valorCrudo ?? '—'}</td>
            </tr>
          ))}
          {errores.length === 0 && <tr><td colSpan={6}>Sin errores auditados.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}
