import { apiDownload, apiRequest, type DownloadedFile } from './client'
import type {
  Credentials,
  ExportResult,
  LibrarySyncResult,
  Playlist,
  PlaylistInput,
  PlaylistSpotifyLink,
  QueryPage,
  SchemaResponse,
  SpotifyConnectResponse,
  SpotifyStatus,
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
  exportSpotify: (id: string, rematch = false) =>
    apiRequest<ExportResult>(`/api/playlists/${id}/export/spotify?rematch=${rematch}`, { method: 'POST' }),
  spotifyLink: (id: string) => apiRequest<PlaylistSpotifyLink>(`/api/playlists/${id}/spotify`),
  setKeepLive: (id: string, enabled: boolean) =>
    apiRequest<PlaylistSpotifyLink>(`/api/playlists/${id}/spotify/keep-live`, {
      method: 'PUT',
      body: { enabled },
    }),
  exportM3u: (id: string, fallbackName: string): Promise<DownloadedFile> =>
    apiDownload(`/api/playlists/${id}/export/m3u`, fallbackName),
}

export const spotify = {
  status: () => apiRequest<SpotifyStatus>('/api/spotify/status'),
  connect: () => apiRequest<SpotifyConnectResponse>('/api/spotify/connect', { method: 'POST' }),
  callback: (state: string, code: string) =>
    apiRequest<SpotifyStatus>('/api/spotify/callback', { method: 'POST', body: { state, code } }),
  disconnect: () => apiRequest<void>('/api/spotify/disconnect', { method: 'POST' }),
  syncLibrary: () => apiRequest<LibrarySyncResult>('/api/spotify/library/sync', { method: 'POST' }),
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
  genres: (q: string, limit = 20, offset = 0) =>
    apiRequest<Suggestion[]>(
      `/api/catalog/genres?q=${encodeURIComponent(q)}&limit=${limit}&offset=${offset}`,
    ),
  artists: (q: string, limit = 20, offset = 0) =>
    apiRequest<Suggestion[]>(
      `/api/catalog/artists?q=${encodeURIComponent(q)}&limit=${limit}&offset=${offset}`,
    ),
}
