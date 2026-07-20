import type { Operator, SchemaEntity } from '../../../api/types'
import { Autocomplete } from '../../../components/ui/Autocomplete'
import { Icon } from '../../../components/ui/Icon'
import { TextInput } from '../../../components/ui/TextInput'
import type { Condition } from '../../../mql/types'
import styles from './builder.module.css'

type ControlKind = 'genre' | 'artist' | 'text' | 'number'

function controlKind(entity: SchemaEntity, field: string): ControlKind {
  const def = entity.fields.find((candidate) => candidate.name === field)
  if (!def) return 'text'
  if (def.type === 'genre') return 'genre'
  if (def.type === 'number') return 'number'
  if (field === 'artist' || (entity.name === 'artists' && field === 'name')) return 'artist'
  return 'text'
}

interface SingleValueProps {
  kind: ControlKind
  value: string
  onChange: (value: string) => void
  placeholder: string
  ariaLabel: string
}

function SingleValue({ kind, value, onChange, placeholder, ariaLabel }: SingleValueProps) {
  if (kind === 'genre' || kind === 'artist') {
    return (
      <Autocomplete
        kind={kind}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        ariaLabel={ariaLabel}
      />
    )
  }
  return (
    <TextInput
      value={value}
      onChange={(event) => onChange(event.target.value)}
      placeholder={placeholder}
      aria-label={ariaLabel}
      inputMode={kind === 'number' ? 'numeric' : undefined}
    />
  )
}

interface ValueEditorProps {
  entity: SchemaEntity
  condition: Condition
  onChange: (patch: Partial<Condition>) => void
}

export function ValueEditor({ entity, condition, onChange }: ValueEditorProps) {
  const kind = controlKind(entity, condition.field)
  const op: Operator = condition.op
  const noun = condition.field || 'value'

  if (op === 'between') {
    return (
      <div className={styles.betweenRow}>
        <SingleValue
          kind={kind}
          value={condition.value}
          onChange={(value) => onChange({ value })}
          placeholder="from"
          ariaLabel={`${noun} lower bound`}
        />
        <span className={styles.betweenAnd}>and</span>
        <SingleValue
          kind={kind}
          value={condition.valueHigh}
          onChange={(valueHigh) => onChange({ valueHigh })}
          placeholder="to"
          ariaLabel={`${noun} upper bound`}
        />
      </div>
    )
  }

  if (op === 'in') {
    return (
      <ValueList kind={kind} noun={noun} values={condition.values} onChange={(values) => onChange({ values })} />
    )
  }

  return (
    <SingleValue
      kind={kind}
      value={condition.value}
      onChange={(value) => onChange({ value })}
      placeholder={kind === 'number' ? '1990' : `Any ${noun}`}
      ariaLabel={`${noun} value`}
    />
  )
}

interface ValueListProps {
  kind: ControlKind
  noun: string
  values: string[]
  onChange: (values: string[]) => void
}

function ValueList({ kind, noun, values, onChange }: ValueListProps) {
  const committed = values.filter((value) => value.trim() !== '')

  function addDraft() {
    onChange([...values, ''])
  }

  function updateAt(index: number, value: string) {
    onChange(values.map((current, i) => (i === index ? value : current)))
  }

  function removeAt(index: number) {
    onChange(values.filter((_, i) => i !== index))
  }

  return (
    <div className={styles.valueList}>
      {values.map((value, index) => (
        <div key={index} className={styles.valueListRow}>
          <SingleValue
            kind={kind}
            value={value}
            onChange={(next) => updateAt(index, next)}
            placeholder={kind === 'number' ? '1990' : `Any ${noun}`}
            ariaLabel={`${noun} value ${index + 1}`}
          />
          <button
            type="button"
            className={styles.valueRemove}
            aria-label="Remove value"
            onClick={() => removeAt(index)}
          >
            <Icon name="x" size={14} />
          </button>
        </div>
      ))}
      <button type="button" className={styles.addValue} onClick={addDraft}>
        <Icon name="plus" size={13} />
        {committed.length === 0 ? 'Add a value' : 'Add another'}
      </button>
    </div>
  )
}
