import type { SchemaEntity } from '../../../api/types'
import { Icon } from '../../../components/ui/Icon'
import { IconButton } from '../../../components/ui/misc'
import type { Combinator, Condition, Group } from '../../../mql/types'
import { ConditionRow } from './ConditionRow'
import styles from './builder.module.css'

export interface GroupHandlers {
  updateCondition: (id: string, patch: Partial<Condition>) => void
  updateCombinator: (id: string, combinator: Combinator) => void
  addCondition: (groupId: string) => void
  addGroup: (groupId: string) => void
  removeNode: (id: string) => void
}

interface GroupEditorProps {
  entity: SchemaEntity
  group: Group
  isRoot: boolean
  handlers: GroupHandlers
}

export function GroupEditor({ entity, group, isRoot, handlers }: GroupEditorProps) {
  function toggleCombinator() {
    handlers.updateCombinator(group.id, group.combinator === 'and' ? 'or' : 'and')
  }

  return (
    <div className={styles.group} data-root={isRoot || undefined}>
      {group.children.map((child, index) => (
        <div className={styles.item} key={child.id}>
          <div className={styles.rail}>
            {index === 0 ? (
              <span className={styles.railLabel}>{isRoot ? 'WHERE' : group.combinator}</span>
            ) : (
              <button
                type="button"
                className={styles.combinator}
                onClick={toggleCombinator}
                title="Toggle AND / OR for this group"
              >
                {group.combinator}
              </button>
            )}
          </div>
          <div className={styles.itemBody}>
            {child.kind === 'condition' ? (
              <ConditionRow
                entity={entity}
                condition={child}
                onChange={(patch) => handlers.updateCondition(child.id, patch)}
                onRemove={() => handlers.removeNode(child.id)}
              />
            ) : (
              <div className={styles.subGroup}>
                <div className={styles.subGroupHead}>
                  <span className={styles.subGroupLabel}>Group</span>
                  <span className={styles.subGroupHint}>
                    {child.combinator === 'and' ? 'match all' : 'match any'}
                  </span>
                  <IconButton
                    icon="trash"
                    label="Remove group"
                    tone="danger"
                    onClick={() => handlers.removeNode(child.id)}
                    className={styles.subGroupRemove}
                  />
                </div>
                <GroupEditor entity={entity} group={child} isRoot={false} handlers={handlers} />
              </div>
            )}
          </div>
        </div>
      ))}

      <div className={styles.addRow}>
        <button type="button" className={styles.addButton} onClick={() => handlers.addCondition(group.id)}>
          <Icon name="plus" size={13} />
          Add filter
        </button>
        <button type="button" className={styles.addButton} onClick={() => handlers.addGroup(group.id)}>
          <Icon name="plus" size={13} />
          Add group
        </button>
      </div>
    </div>
  )
}
