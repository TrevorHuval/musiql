import { Select, type SelectOption } from '../../../components/ui/Select'
import { Segmented } from '../../../components/ui/Segmented'
import { TextInput } from '../../../components/ui/TextInput'
import { Icon } from '../../../components/ui/Icon'
import { IconButton } from '../../../components/ui/misc'
import { newSortKey, type SortDirection, type SortKey } from '../../../mql/types'
import styles from './builder.module.css'

function capitalize(word: string): string {
  return word.charAt(0).toUpperCase() + word.slice(1)
}

interface SortEditorProps {
  orderable: string[]
  sort: SortKey[]
  onChange: (sort: SortKey[]) => void
}

export function SortEditor({ orderable, sort, onChange }: SortEditorProps) {
  const fieldOptions: SelectOption<string>[] = orderable.map((field) => ({
    value: field,
    label: capitalize(field),
  }))

  function update(id: string, patch: Partial<SortKey>) {
    onChange(sort.map((key) => (key.id === id ? { ...key, ...patch } : key)))
  }

  return (
    <div className={styles.sortList}>
      {sort.map((key) => (
        <div className={styles.sortRow} key={key.id}>
          <Select
            value={key.field}
            options={fieldOptions}
            onChange={(field) => update(key.id, { field })}
            ariaLabel="Sort field"
            size="sm"
          />
          <Segmented<SortDirection>
            value={key.direction}
            onChange={(direction) => update(key.id, { direction })}
            ariaLabel="Sort direction"
            size="sm"
            options={[
              { value: 'asc', label: 'Asc', icon: 'arrow-up' },
              { value: 'desc', label: 'Desc', icon: 'arrow-down' },
            ]}
          />
          <IconButton icon="x" label="Remove sort" onClick={() => onChange(sort.filter((k) => k.id !== key.id))} />
        </div>
      ))}
      {orderable.length > 0 && (
        <button
          type="button"
          className={styles.addButton}
          onClick={() => onChange([...sort, newSortKey(orderable[0])])}
        >
          <Icon name="plus" size={13} />
          Add sort
        </button>
      )}
    </div>
  )
}

interface LimitControlProps {
  limit: number | null
  onChange: (limit: number | null) => void
}

export function LimitControl({ limit, onChange }: LimitControlProps) {
  return (
    <div className={styles.limitRow}>
      <TextInput
        className={styles.limitInput}
        inputMode="numeric"
        placeholder="100"
        aria-label="Result limit"
        value={limit == null ? '' : String(limit)}
        onChange={(event) => {
          const digits = event.target.value.replace(/[^0-9]/g, '')
          onChange(digits === '' ? null : Math.min(Number(digits), 500))
        }}
      />
      <span className={styles.limitHint}>tracks · up to 500, defaults to 100</span>
    </div>
  )
}
