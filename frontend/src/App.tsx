import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { Navbar } from './components/layout/Navbar'
import { HomePage } from './pages/HomePage'
import { StreamPage } from './pages/StreamPage'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { DashboardPage } from './pages/DashboardPage'
import { AdminPage } from './pages/AdminPage'
import { VodPage } from './pages/VodPage'
import { VodsListPage } from './pages/VodsListPage'
import { ClipsPage } from './pages/ClipsPage'
import { ClipPlayerPage } from './pages/ClipPlayerPage'
import { useAuthStore } from './store/authStore'
import './styles/globals.css'
import styles from './App.module.css'

function PrivateRoute({ children, admin = false }: { children: React.ReactNode; admin?: boolean }) {
  const { user } = useAuthStore()
  if (!user) return <Navigate to="/login" replace />
  if (admin && !user.isAdmin) return <Navigate to="/" replace />
  return <>{children}</>
}

export default function App() {
  return (
    <BrowserRouter>
      <div className={styles.app}>
        <Navbar />
        <main className={styles.main}>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/stream/:username" element={<StreamPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/dashboard" element={
              <PrivateRoute><DashboardPage /></PrivateRoute>
            } />
            <Route path="/admin" element={
              <PrivateRoute admin><AdminPage /></PrivateRoute>
            } />
            {/* VOD */}
            <Route path="/vods" element={<VodsListPage />} />
            <Route path="/vod/:id" element={<VodPage />} />
            {/* Clips */}
            <Route path="/clips" element={<ClipsPage />} />
            <Route path="/clips/:id" element={<ClipPlayerPage />} />
            {/* Fallback */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  )
}
