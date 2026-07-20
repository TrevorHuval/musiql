import type { Operator, SchemaEntity } from '../../../api/types'
import { Select, type SelectOption } from '../../../components/ui/Select'
import { IconButton } from '../../../components/ui/misc'
import { OPERATOR_LABELS } from '../../../mql/schema'
import type { Condition } from '../../../mql/types'
import { ValueEditor } from './ValueEditor'
import styles from './builder.module.css'

interface ConditionRowProps {
  entity: SchemaEntity
  condition: Condition
  onChange: (patch: Partial<Condition>) => void
  onRemove: () => void
}

function capitalize(word: string): string {
  return word.charAt(0).toUpperCase() + word.slice(1)
}

export function ConditionRow({ entity, condition, onChange, onRemove }: ConditionRowProps) {
  const fieldOptions: SelectOption<string>[] = entity.fields.map((field) => ({
    value: field.name,
    label: capitalize(field.name),
  }))

  const activeField = entity.fields.find((field) => field.name === condition.field)
  const operatorOptions: SelectOption<Operator>[] = (activeField?.operators ?? []).map((op) => ({
    value: op,
    label: OPERATOR_LABELS[op],
  }))

  function handleFieldChange(field: string) {
    const nextField = entity.fields.find((candidate) => candidate.name === field)
    const nextOp = nextField?.operators.includes(condition.op)
      ? condition.op
      : (nextField?.operators[0] ?? '=')
    onChange({ field, op: nextOp, value: '', valueHigh: '', values: [] })
  }

  function handleOperatorChange(op: Operator) {
    onChange({ op, value: '', valueHigh: '', values: [] })
  }

  return (
    <div className={styles.condition}>
      <div className={styles.conditionControls}>
        <Select
          value={condition.field}
          options={fieldOptions}
          onChange={handleFieldChange}
          ariaLabel="Field"
        />
        <Select
          value={condition.op}
          options={operatorOptions}
          onChange={handleOperatorChange}
          ariaLabel="Operator"
        />
      </div>
      <div className={styles.conditionValue}>
        {activeField ? (
          <ValueEditor entity={entity} condition={condition} onChange={onChange} />
        ) : null}
      </div>
      <IconButton icon="x" label="Remove filter" onClick={onRemove} className={styles.conditionRemove} />
    </div>
  )
}
