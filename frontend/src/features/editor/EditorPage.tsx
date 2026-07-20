import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { problemToMessage } from '../../api/problem'
import { queryApi, useExportM3u, useSchema, useUpdatePlaylist, usePlaylist } from '../../api/queries'
import { Callout } from '../../components/ui/Callout'
import { Icon } from '../../components/ui/Icon'
import { Spinner } from '../../components/ui/misc'
import { useDebounced } from '../../lib/useDebounced'
import { generateMql } from '../../mql/generate'
import { parseMql } from '../../mql/parse'
import { ENTITY_LABELS, findEntity, resolverFor } from '../../mql/schema'
import { emptyState, type BuilderState } from '../../mql/types'
import { BuilderPanel } from './builder/BuilderPanel'
import { AdvancedPanel } from './advanced/AdvancedPanel'
import { EditorToolbar, type EditorMode } from './EditorToolbar'
import { ExportSpotifyDialog } from './ExportSpotifyDialog'
import { ResultsPanel, type ResultsView } from './results/ResultsPanel'
import { SavePlaylistDialog } from './SavePlaylistDialog'
import { RenameDialog } from '../library/RenameDialog'
import styles from './editor.module.css'

const PAGE_SIZE = 50

export function EditorPage() {
  const { id } = useParams<{ id: string }>()
  const isNew = id === undefined
  const schema = useSchema()
  const playlist = usePlaylist(id ?? '')
  const shouldWait = schema.isPending || (!isNew && playlist.isPending)

  if (shouldWait) {
    return (
      <div className={styles.loading}>
        <Spinner />
      </div>
    )
  }

  if (schema.isError) {
    return (
      <div className={styles.errorState}>
        <Callout tone="clay" icon="x" title="Couldn’t load the query builder">
          {problemToMessage(schema.error)}
        </Callout>
      </div>
    )
  }

  if (!isNew && playlist.isError) {
    return (
      <div className={styles.errorState}>
        <Callout tone="clay" icon="x" title="Playlist not found">
          This playlist doesn’t exist, or it isn’t yours.
        </Callout>
      </div>
    )
  }

  return (
    <Editor
      key={id ?? 'new'}
      schema={schema.data}
      playlistId={id}
      initialMql={playlist.data?.mql}
      playlistName={playlist.data?.name}
      playlistDescription={playlist.data?.description ?? null}
    />
  )
}

interface EditorProps {
  schema: import('../../api/types').SchemaResponse
  playlistId?: string
  initialMql?: string
  playlistName?: string
  playlistDescription?: string | null
}

function Editor({ schema, playlistId, initialMql, playlistName, playlistDescription }: EditorProps) {
  const isNew = playlistId === undefined
  const update = useUpdatePlaylist(playlistId ?? '')
  const exportM3u = useExportM3u(playlistId ?? '', playlistName ?? 'playlist')

  const initial = useMemo(() => deriveInitial(initialMql), [initialMql])
  const [mode, setMode] = useState<EditorMode>(initial.mode)
  const [builder, setBuilder] = useState<BuilderState>(initial.builder)
  const [mqlText, setMqlText] = useState(initial.text)
  const [savedMql, setSavedMql] = useState(initialMql ?? '')
  const [switchError, setSwitchError] = useState<string | null>(null)
  const [saveOpen, setSaveOpen] = useState(false)
  const [renameOpen, setRenameOpen] = useState(false)
  const [exportOpen, setExportOpen] = useState(false)
  const [page, setPage] = useState(1)

  const entityDef = findEntity(schema, builder.entity)
  const resolve = useMemo(() => resolverFor(entityDef), [entityDef])
  const currentMql = mode === 'builder' ? generateMql(builder, resolve) : mqlText.trim()

  const debounced = useDebounced(currentMql, 350)
  const lastMql = useRef(currentMql)
  useEffect(() => {
    if (debounced !== lastMql.current) {
      lastMql.current = debounced
      setPage(1)
    }
  }, [debounced])

  const preview = useQuery({
    queryKey: ['preview', debounced, page],
    queryFn: () => queryApi.preview({ mql: debounced, page, pageSize: PAGE_SIZE }),
    enabled: debounced !== '',
    placeholderData: keepPreviousData,
    retry: false,
  })

  const mqlErrors =
    preview.error instanceof ApiError ? (preview.error.mqlErrors ?? []) : []
  const view = deriveView({
    debounced,
    isError: preview.isError,
    error: preview.error,
    data: preview.data,
    hasMqlErrors: mqlErrors.length > 0,
  })
  const entityLabel = ENTITY_LABELS[builder.entity]

  function switchMode(next: EditorMode) {
    if (next === mode) return
    if (next === 'advanced') {
      setMqlText(generateMql(builder, resolve))
      setSwitchError(null)
      setMode('advanced')
      return
    }
    const parsed = parseMql(mqlText)
    if (parsed.ok) {
      setBuilder(parsed.state)
      setSwitchError(null)
      setMode('builder')
    } else {
      setSwitchError(parsed.reason)
    }
  }

  async function handleSave() {
    if (isNew) {
      setSaveOpen(true)
      return
    }
    try {
      await update.mutateAsync({
        name: playlistName ?? 'Untitled',
        description: playlistDescription ?? undefined,
        mql: currentMql,
      })
      setSavedMql(currentMql)
    } catch {
      // Surfaced by the toolbar staying dirty; a full toast system is out of scope for now.
    }
  }

  const dirty = currentMql !== savedMql

  return (
    <div className={styles.page}>
      <EditorToolbar
        title={isNew ? 'New playlist' : (playlistName ?? 'Playlist')}
        isNew={isNew}
        mode={mode}
        onModeChange={switchMode}
        dirty={dirty}
        saving={update.isPending}
        onSave={handleSave}
        onRename={isNew ? undefined : () => setRenameOpen(true)}
        onExport={isNew ? undefined : () => setExportOpen(true)}
        onDownloadM3u={isNew ? undefined : () => exportM3u.mutate()}
        downloadingM3u={exportM3u.isPending}
      />

      {exportM3u.isError && (
        <div className={styles.switchNote}>
          <Callout tone="clay" icon="x" title="Couldn’t build the M3U file">
            {problemToMessage(exportM3u.error)}
          </Callout>
        </div>
      )}

      {switchError && (
        <div className={styles.switchNote}>
          <Callout tone="signal" icon="sliders" title="This query can’t open in the builder">
            {switchError} You can keep editing it in advanced mode.
          </Callout>
        </div>
      )}

      <div className={styles.deck}>
        {mode === 'builder' ? (
          <BuilderPanel schema={schema} state={builder} onChange={setBuilder} />
        ) : (
          <AdvancedPanel value={mqlText} onChange={setMqlText} errors={mqlErrors} />
        )}
      </div>

      {mode === 'builder' && (
        <button type="button" className={styles.readout} onClick={() => switchMode('advanced')}>
          <span className={styles.readoutLabel}>MQL</span>
          <code className={styles.readoutText}>{currentMql}</code>
          <span className={styles.readoutHint}>
            Edit as text
            <Icon name="code" size={13} />
          </span>
        </button>
      )}

      <ResultsPanel view={view} entityLabel={entityLabel} busy={preview.isFetching} onPageChange={setPage} />

      {saveOpen && <SavePlaylistDialog mql={currentMql} open onClose={() => setSaveOpen(false)} />}
      {exportOpen && playlistId && (
        <ExportSpotifyDialog
          playlistId={playlistId}
          playlistName={playlistName ?? 'Playlist'}
          open
          onClose={() => setExportOpen(false)}
        />
      )}
      {renameOpen && playlistId && (
        <RenameDialog
          playlist={{
            id: playlistId,
            name: playlistName ?? '',
            description: playlistDescription ?? null,
            mql: currentMql,
            createdAt: '',
            updatedAt: '',
          }}
          open
          onClose={() => setRenameOpen(false)}
        />
      )}
    </div>
  )
}

function deriveInitial(mql: string | undefined): {
  mode: EditorMode
  builder: BuilderState
  text: string
} {
  if (!mql) return { mode: 'builder', builder: emptyState('tracks'), text: '' }
  const parsed = parseMql(mql)
  if (parsed.ok) return { mode: 'builder', builder: parsed.state, text: mql }
  return { mode: 'advanced', builder: emptyState('tracks'), text: mql }
}

interface ViewInputs {
  debounced: string
  isError: boolean
  error: unknown
  data: import('../../api/types').QueryPage | undefined
  hasMqlErrors: boolean
}

function deriveView({ debounced, isError, error, data, hasMqlErrors }: ViewInputs): ResultsView {
  if (debounced === '') return { kind: 'empty-query' }
  if (isError) {
    if (hasMqlErrors) return { kind: 'invalid' }
    return { kind: 'error', message: problemToMessage(error) }
  }
  if (!data) return { kind: 'loading' }
  if (data.hint === 'library_empty') return { kind: 'library-empty' }
  if (data.total === 0) return { kind: 'no-results' }
  return { kind: 'results', page: data }
}
