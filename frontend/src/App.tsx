import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { RutaProtegida } from './auth/RutaProtegida';
import { Login } from './pages/Login';
import { Upload } from './pages/Upload';
import { Historial } from './pages/Historial';
import { Detalle } from './pages/Detalle';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route element={<RutaProtegida />}>
            <Route path="/" element={<Navigate to="/historial" replace />} />
            <Route path="/subir" element={<Upload />} />
            <Route path="/historial" element={<Historial />} />
            <Route path="/cargas/:id" element={<Detalle />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
