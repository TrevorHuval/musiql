import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { Icon } from '../../components/ui/Icon'
import { IconButton, LivePill } from '../../components/ui/misc'
import { Segmented } from '../../components/ui/Segmented'
import styles from './editor.module.css'

export type EditorMode = 'builder' | 'advanced'

interface EditorToolbarProps {
  title: string
  isNew: boolean
  mode: EditorMode
  onModeChange: (mode: EditorMode) => void
  dirty: boolean
  saving: boolean
  onSave: () => void
  onRename?: () => void
  onExport?: () => void
}

export function EditorToolbar({
  title,
  isNew,
  mode,
  onModeChange,
  dirty,
  saving,
  onSave,
  onRename,
  onExport,
}: EditorToolbarProps) {
  return (
    <div className={styles.toolbar}>
      <div className={styles.toolbarLeft}>
        <Link to="/" className={styles.back} aria-label="Back to your playlists">
          <Icon name="chevron-right" size={18} className={styles.backIcon} />
        </Link>
        <div className={styles.titleBlock}>
          <div className={styles.titleLine}>
            <h1 className={styles.title}>{title}</h1>
            {onRename && <IconButton icon="pencil" label="Rename playlist" onClick={onRename} />}
          </div>
          {!isNew && <LivePill />}
        </div>
      </div>

      <div className={styles.toolbarRight}>
        <Segmented<EditorMode>
          value={mode}
          onChange={onModeChange}
          ariaLabel="Editing mode"
          options={[
            { value: 'builder', label: 'Builder', icon: 'sliders' },
            { value: 'advanced', label: 'Advanced', icon: 'code' },
          ]}
        />
        {onExport && (
          <Button variant="secondary" onClick={onExport}>
            <span className={styles.exportLabel}>
              <Icon name="link" size={15} />
              Export
            </span>
          </Button>
        )}
        <Button variant="primary" onClick={onSave} loading={saving} disabled={!isNew && !dirty}>
          {isNew ? 'Save playlist' : dirty ? 'Save changes' : 'Saved'}
        </Button>
      </div>
    </div>
  )
}
