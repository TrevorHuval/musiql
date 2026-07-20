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
