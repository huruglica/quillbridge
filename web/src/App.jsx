import { Suspense, lazy } from 'react';
import { Navigate, Route, Routes } from 'react-router';
import { useAuth } from './lib/auth';
import Login from './pages/Login';

// the login screen ships alone; the app loads once someone is signed in
const AppShell = lazy(() => import('./components/AppShell'));
const CallList = lazy(() => import('./pages/CallList'));
const Guide = lazy(() => import('./pages/Guide'));
const DoNotCall = lazy(() => import('./pages/DoNotCall'));
const Import = lazy(() => import('./pages/Import'));
const Insights = lazy(() => import('./pages/Insights'));
const NotFound = lazy(() => import('./pages/NotFound'));

export default function App() {
  const { isAuthenticated } = useAuth();

  if (!isAuthenticated) {
    return (
      <Routes>
        <Route path="*" element={<Login />} />
      </Routes>
    );
  }

  return (
    <Suspense fallback={null}>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/calls" replace />} />
          <Route path="calls" element={<CallList />} />
          <Route path="calls/:prospectId" element={<Guide />} />
          <Route path="do-not-call" element={<DoNotCall />} />
          <Route path="import" element={<Import />} />
          <Route path="insights" element={<Insights />} />
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
  );
}
