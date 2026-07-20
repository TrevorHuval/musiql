import { Navigate, Route, Routes } from 'react-router-dom'
import { GuestRoute, ProtectedRoute } from '../auth/ProtectedRoute'
import { LoginPage } from '../features/auth/LoginPage'
import { RegisterPage } from '../features/auth/RegisterPage'
import { LibraryPage } from '../features/library/LibraryPage'
import { EditorPage } from '../features/editor/EditorPage'
import { SettingsPage } from '../features/settings/SettingsPage'
import { SpotifyCallbackPage } from '../features/settings/SpotifyCallbackPage'
import { AppShell } from './AppShell'

export function App() {
  return (
    <Routes>
      <Route element={<GuestRoute />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<LibraryPage />} />
          <Route path="/playlists/new" element={<EditorPage />} />
          <Route path="/playlists/:id" element={<EditorPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/settings/spotify/callback" element={<SpotifyCallbackPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
