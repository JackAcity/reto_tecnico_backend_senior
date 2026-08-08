import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';
import { NavBar } from '../components/NavBar';

export function RutaProtegida() {
  const { autenticado } = useAuth();

  if (!autenticado) return <Navigate to="/login" replace />;

  return (
    <>
      <NavBar />
      <main className="contenedor">
        <Outlet />
      </main>
    </>
  );
}
