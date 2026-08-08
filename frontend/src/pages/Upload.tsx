import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { subirArchivo, ApiError, type ResultadoRegistro } from '../api/client';

export function Upload() {
  const navigate = useNavigate();
  const [archivo, setArchivo] = useState<File | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [resultado, setResultado] = useState<ResultadoRegistro | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!archivo) return;

    setEnviando(true);
    setError(null);
    setResultado(null);
    try {
      setResultado(await subirArchivo(archivo));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Error inesperado subiendo el archivo.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="tarjeta">
      <h2>Subir Excel</h2>
      <p className="subtitulo">Solo <code>.xlsx</code>. Se valida extensión, tamaño y firma binaria.</p>

      <form onSubmit={enviar} className="form-subida">
        <input
          type="file"
          accept=".xlsx"
          onChange={(e) => setArchivo(e.target.files?.[0] ?? null)}
        />
        <button type="submit" disabled={!archivo || enviando}>
          {enviando ? 'Subiendo…' : 'Subir'}
        </button>
      </form>

      {error && <p role="alert" className="error">{error}</p>}

      {resultado && (
        <div className={resultado.error ? 'aviso aviso-error' : 'aviso aviso-ok'} role="status">
          <p>Carga #{resultado.idCarga} — estado <strong>{resultado.estado}</strong></p>
          {resultado.error && <p>{resultado.error}</p>}
          <button type="button" onClick={() => navigate(`/cargas/${resultado.idCarga}`)}>
            Ver detalle
          </button>
        </div>
      )}
    </div>
  );
}
