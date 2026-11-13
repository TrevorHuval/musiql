import type { TokenPair } from './types'

const STORAGE_KEY = 'musiql.auth'

let current: TokenPair | null = read()
const listeners = new Set<() => void>()

function read(): TokenPair | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as TokenPair) : null
  } catch {
    return null
  }
}

function emit() {
  for (const listener of listeners) listener()
}

// Another tab may have rotated the tokens; re-read before relying on the in-memory copy.
export function reloadAuth(): TokenPair | null {
  current = read()
  return current
}

window.addEventListener('storage', (e) => {
  if (e.key === STORAGE_KEY || e.key === null) {
    current = read()
    emit()
  }
})

export function getAuth(): TokenPair | null {
  return current
}

export function setAuth(tokens: TokenPair): void {
  current = tokens
  localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens))
  emit()
}

export function clearAuth(): void {
  current = null
  localStorage.removeItem(STORAGE_KEY)
  emit()
}

export function subscribeAuth(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
