import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AuthContext } from './AuthContext'
import { GuestRoute, ProtectedRoute } from './ProtectedRoute'

type User = { id: string; email: string } | null

function renderAt(path: string, user: User) {
  const value = {
    user,
    register: async () => {},
    login: async () => {},
    logout: async () => {},
  }
  return render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/" element={<p>library</p>} />
          </Route>
          <Route element={<GuestRoute />}>
            <Route path="/login" element={<p>sign in</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('route guards', () => {
  it('sends a signed-out visitor from a protected route to login', () => {
    renderAt('/', null)
    expect(screen.getByText('sign in')).toBeInTheDocument()
    expect(screen.queryByText('library')).not.toBeInTheDocument()
  })

  it('lets a signed-in user reach a protected route', () => {
    renderAt('/', { id: 'u1', email: 'you@example.com' })
    expect(screen.getByText('library')).toBeInTheDocument()
  })

  it('keeps a signed-in user out of the guest login route', () => {
    renderAt('/login', { id: 'u1', email: 'you@example.com' })
    expect(screen.getByText('library')).toBeInTheDocument()
    expect(screen.queryByText('sign in')).not.toBeInTheDocument()
  })
})
