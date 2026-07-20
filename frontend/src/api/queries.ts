import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { catalog, playlists, type PreviewInput } from './endpoints'
import { query as queryApi } from './endpoints'
import type { PlaylistInput } from './types'

export const keys = {
  schema: ['schema'] as const,
  playlists: ['playlists'] as const,
  playlist: (id: string) => ['playlists', id] as const,
  tracks: (id: string, page: number, pageSize: number) =>
    ['playlists', id, 'tracks', page, pageSize] as const,
  preview: (input: PreviewInput) => ['preview', input] as const,
}

export function useSchema() {
  return useQuery({
    queryKey: keys.schema,
    queryFn: catalog.fields,
    staleTime: Infinity,
  })
}

export function usePlaylists() {
  return useQuery({ queryKey: keys.playlists, queryFn: playlists.list })
}

export function usePlaylist(id: string) {
  return useQuery({ queryKey: keys.playlist(id), queryFn: () => playlists.get(id) })
}

export function useCreatePlaylist() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: PlaylistInput) => playlists.create(input),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.playlists }),
  })
}

export function useUpdatePlaylist(id: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: PlaylistInput) => playlists.update(id, input),
    onSuccess: (updated) => {
      client.setQueryData(keys.playlist(id), updated)
      client.invalidateQueries({ queryKey: keys.playlists })
    },
  })
}

export function useDeletePlaylist() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => playlists.remove(id),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.playlists }),
  })
}

export function usePlaylistTracks(id: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: keys.tracks(id, page, pageSize),
    queryFn: () => playlists.tracks(id, page, pageSize),
  })
}

export { queryApi }
