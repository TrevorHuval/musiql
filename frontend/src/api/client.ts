import { clearAuth, getAuth, setAuth } from './authStore'
import type { MqlError, ProblemDetails, TokenPair } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, message: string, problem: ProblemDetails | null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get mqlErrors(): MqlError[] | null {
    const errors = this.problem?.errors
    if (Array.isArray(errors)) return errors
    return null
  }

  get fieldErrors(): Record<string, string[]> | null {
    const errors = this.problem?.errors
    if (errors && !Array.isArray(errors)) return errors
    return null
  }
}

interface RequestOptions {
  method?: string
  body?: unknown
  auth?: boolean
  signal?: AbortSignal
}

let refreshInFlight: Promise<TokenPair | null> | null = null

// Vite's BASE_URL is the path prefix the app is deployed under ("/" locally,
// "/musiql/" behind the reverse proxy). API routes live under the same prefix.
const basePath = import.meta.env.BASE_URL.replace(/\/$/, '')

export function apiUrl(path: string): string {
  return `${basePath}${path}`
}

async function refreshTokens(): Promise<TokenPair | null> {
  const existing = getAuth()
  if (!existing) return null

  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      try {
        const response = await fetch(apiUrl('/api/auth/refresh'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: existing.refreshToken }),
        })
        if (!response.ok) {
          clearAuth()
          return null
        }
        const tokens = (await response.json()) as TokenPair
        setAuth(tokens)
        return tokens
      } catch {
        return null
      } finally {
        refreshInFlight = null
      }
    })()
  }

  return refreshInFlight
}

async function parseProblem(response: Response): Promise<ProblemDetails | null> {
  const type = response.headers.get('content-type') ?? ''
  if (!type.includes('json')) return null
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

function problemMessage(status: number, problem: ProblemDetails | null): string {
  if (problem?.title) return problem.title
  if (problem?.detail) return problem.detail
  return `Request failed (${status})`
}

async function authorizedFetch(
  path: string,
  options: RequestOptions,
  retrying: boolean,
): Promise<Response> {
  const { method = 'GET', body, auth = true, signal } = options
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  if (auth) {
    const tokens = getAuth()
    if (tokens) headers.Authorization = `${tokens.tokenType} ${tokens.accessToken}`
  }

  const response = await fetch(apiUrl(path), {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  })

  if (response.status === 401 && auth && !retrying) {
    const refreshed = await refreshTokens()
    if (refreshed) return authorizedFetch(path, options, true)
  }

  if (!response.ok) {
    const problem = await parseProblem(response)
    if (response.status === 401 && auth) clearAuth()
    throw new ApiError(response.status, problemMessage(response.status, problem), problem)
  }

  return response
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await authorizedFetch(path, options, false)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export interface DownloadedFile {
  blob: Blob
  filename: string
}

export async function apiDownload(path: string, fallbackName: string): Promise<DownloadedFile> {
  const response = await authorizedFetch(path, {}, false)
  const blob = await response.blob()
  return { blob, filename: filenameFrom(response.headers.get('content-disposition'), fallbackName) }
}

function filenameFrom(disposition: string | null, fallback: string): string {
  if (!disposition) return fallback
  const encoded = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(disposition)
  if (encoded) return decodeURIComponent(encoded[1].replace(/"/g, '').trim())
  const plain = /filename="?([^";]+)"?/i.exec(disposition)
  return plain ? plain[1].trim() : fallback
}
