import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { obtenerHistorial, ApiError, type ResumenCarga } from '../api/client';
import { EstadoBadge } from '../components/EstadoBadge';

const ESTADOS_TERMINALES = new Set(['Notificado', 'Rechazada', 'Bloqueada', 'Fallida']);
const INTERVALO_POLLING_MS = 3000;

export function Historial() {
  const [cargas, setCargas] = useState<ResumenCarga[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  const cargar = useCallback(async () => {
    try {
      setCargas(await obtenerHistorial());
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No se pudo cargar el historial.');
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  // §3/5️⃣ del enunciado: "ver estados en tiempo real mediante pooling" — sin
  // WebSockets, solo re-consulta mientras algo siga sin llegar a estado terminal.
  useEffect(() => {
    const hayPendientes = cargas.some((c) => !ESTADOS_TERMINALES.has(c.estado));
    if (!hayPendientes) return;
    const id = setInterval(cargar, INTERVALO_POLLING_MS);
    return () => clearInterval(id);
  }, [cargas, cargar]);

  return (
    <div className="tarjeta">
      <div className="encabezado-tarjeta">
        <h2>Historial de cargas</h2>
        <Link to="/subir" className="boton">Nueva carga</Link>
      </div>

      {error && <p role="alert" className="error">{error}</p>}

      {cargando ? (
        <p>Cargando…</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>#</th>
              <th>Archivo</th>
              <th>Usuario</th>
              <th>Fecha</th>
              <th>Estado</th>
              <th>Insertadas</th>
              <th>Rechazadas</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {cargas.map((c) => (
              <tr key={c.idCarga}>
                <td>{c.idCarga}</td>
                <td>{c.nombreArchivo}</td>
                <td>{c.usuario}</td>
                <td>{new Date(c.fechaRegistro).toLocaleString()}</td>
                <td><EstadoBadge estado={c.estado} /></td>
                <td>{c.filasInsertadas}</td>
                <td>{c.filasRechazadas}</td>
                <td><Link to={`/cargas/${c.idCarga}`}>Detalle</Link></td>
              </tr>
            ))}
            {cargas.length === 0 && (
              <tr><td colSpan={8}>Sin cargas todavía.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}
