import { apiRequest } from './client'
import type {
  Credentials,
  Playlist,
  PlaylistInput,
  QueryPage,
  SchemaResponse,
  Suggestion,
  TokenPair,
} from './types'

export const auth = {
  register: (body: Credentials) =>
    apiRequest<TokenPair>('/api/auth/register', { method: 'POST', body, auth: false }),
  login: (body: Credentials) =>
    apiRequest<TokenPair>('/api/auth/login', { method: 'POST', body, auth: false }),
  logout: (refreshToken: string) =>
    apiRequest<void>('/api/auth/logout', { method: 'POST', body: { refreshToken }, auth: false }),
}

export const playlists = {
  list: () => apiRequest<Playlist[]>('/api/playlists'),
  get: (id: string) => apiRequest<Playlist>(`/api/playlists/${id}`),
  create: (body: PlaylistInput) =>
    apiRequest<Playlist>('/api/playlists', { method: 'POST', body }),
  update: (id: string, body: PlaylistInput) =>
    apiRequest<Playlist>(`/api/playlists/${id}`, { method: 'PUT', body }),
  remove: (id: string) => apiRequest<void>(`/api/playlists/${id}`, { method: 'DELETE' }),
  tracks: (id: string, page: number, pageSize: number) =>
    apiRequest<QueryPage>(`/api/playlists/${id}/tracks?page=${page}&pageSize=${pageSize}`),
}

export interface PreviewInput {
  mql: string
  page?: number
  pageSize?: number
}

export const query = {
  preview: (body: PreviewInput, signal?: AbortSignal) =>
    apiRequest<QueryPage>('/api/query/preview', { method: 'POST', body, signal }),
}

export const catalog = {
  fields: () => apiRequest<SchemaResponse>('/api/catalog/fields'),
  genres: (q: string, limit = 20) =>
    apiRequest<Suggestion[]>(`/api/catalog/genres?q=${encodeURIComponent(q)}&limit=${limit}`),
  artists: (q: string, limit = 20) =>
    apiRequest<Suggestion[]>(`/api/catalog/artists?q=${encodeURIComponent(q)}&limit=${limit}`),
}
