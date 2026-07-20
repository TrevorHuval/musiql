import type { SchemaResponse } from '../../../api/types'
import { Segmented } from '../../../components/ui/Segmented'
import { ENTITY_LABELS, findEntity, orderableFields } from '../../../mql/schema'
import {
  newCondition,
  newGroup,
  type BuilderState,
  type Combinator,
  type Condition,
  type Entity,
  type Group,
  type SortKey,
} from '../../../mql/types'
import { addToGroup, removeNode, updateCondition, updateGroup } from '../../../mql/tree'
import { GroupEditor, type GroupHandlers } from './GroupEditor'
import { LimitControl, SortEditor } from './RefineControls'
import styles from './builder.module.css'

interface BuilderPanelProps {
  schema: SchemaResponse
  state: BuilderState
  onChange: (state: BuilderState) => void
}

export function BuilderPanel({ schema, state, onChange }: BuilderPanelProps) {
  const entity = findEntity(schema, state.entity)

  const handlers: GroupHandlers = {
    updateCondition: (id, patch: Partial<Condition>) =>
      onChange({ ...state, root: updateCondition(state.root, id, patch) }),
    updateCombinator: (id, combinator: Combinator) =>
      onChange({ ...state, root: updateGroup(state.root, id, { combinator }) }),
    addCondition: (groupId) => {
      const field = entity?.fields[0]
      const condition = newCondition(field?.name ?? '', field?.operators[0] ?? '=')
      onChange({ ...state, root: addToGroup(state.root, groupId, condition) })
    },
    addGroup: (groupId) =>
      onChange({ ...state, root: addToGroup(state.root, groupId, seedGroup(entity)) }),
    removeNode: (id) => onChange({ ...state, root: removeNode(state.root, id) }),
  }

  function changeEntity(next: Entity) {
    onChange({ ...state, entity: next, root: newGroup('and'), sort: [] })
  }

  function changeSort(sort: SortKey[]) {
    onChange({ ...state, sort })
  }

  function changeLimit(limit: number | null) {
    onChange({ ...state, limit })
  }

  if (!entity) return null

  return (
    <div className={styles.panel}>
      <section className={styles.section}>
        <div className={styles.sectionHead}>
          <span className={styles.sectionLabel}>Source</span>
        </div>
        <div className={styles.sourceRow}>
          <Segmented<Entity>
            value={state.entity}
            onChange={changeEntity}
            ariaLabel="What to list"
            options={[
              { value: 'tracks', label: ENTITY_LABELS.tracks },
              { value: 'albums', label: ENTITY_LABELS.albums },
              { value: 'artists', label: ENTITY_LABELS.artists },
            ]}
          />
          {entity.supportsLibrary && (
            <Segmented<'all' | 'library'>
              value={state.fromLibrary ? 'library' : 'all'}
              onChange={(value) => onChange({ ...state, fromLibrary: value === 'library' })}
              ariaLabel="Catalog scope"
              options={[
                { value: 'all', label: 'All music' },
                { value: 'library', label: 'My library', icon: 'library' },
              ]}
            />
          )}
        </div>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHead}>
          <span className={styles.sectionLabel}>Filters</span>
        </div>
        <GroupEditor entity={entity} group={state.root} isRoot handlers={handlers} />
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHead}>
          <span className={styles.sectionLabel}>Order &amp; limit</span>
        </div>
        <SortEditor orderable={orderableFields(entity)} sort={state.sort} onChange={changeSort} />
        <LimitControl limit={state.limit} onChange={changeLimit} />
      </section>
    </div>
  )
}

function seedGroup(entity: ReturnType<typeof findEntity>): Group {
  const group = newGroup('or')
  const field = entity?.fields[0]
  group.children = [newCondition(field?.name ?? '', field?.operators[0] ?? '=')]
  return group
}
