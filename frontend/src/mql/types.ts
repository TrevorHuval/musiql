import type { FieldType, Operator } from '../api/types'

export type Entity = 'tracks' | 'albums' | 'artists'

export type Combinator = 'and' | 'or'

export type SortDirection = 'asc' | 'desc'

export interface Condition {
  id: string
  kind: 'condition'
  field: string
  op: Operator
  value: string
  valueHigh: string
  values: string[]
}

export interface Group {
  id: string
  kind: 'group'
  combinator: Combinator
  children: BuilderNode[]
}

export type BuilderNode = Condition | Group

export interface SortKey {
  id: string
  field: string
  direction: SortDirection
}

export interface BuilderState {
  entity: Entity
  fromLibrary: boolean
  root: Group
  sort: SortKey[]
  limit: number | null
}

export type FieldTypeResolver = (field: string) => FieldType | undefined

let counter = 0

function nextId(prefix: string): string {
  counter += 1
  return `${prefix}-${counter}`
}

export function newCondition(field = '', op: Operator = '='): Condition {
  return { id: nextId('c'), kind: 'condition', field, op, value: '', valueHigh: '', values: [] }
}

export function newGroup(combinator: Combinator = 'and'): Group {
  return { id: nextId('g'), kind: 'group', combinator, children: [] }
}

export function newSortKey(field: string, direction: SortDirection = 'asc'): SortKey {
  return { id: nextId('s'), field, direction }
}

export function emptyState(entity: Entity = 'tracks'): BuilderState {
  return { entity, fromLibrary: false, root: newGroup('and'), sort: [], limit: null }
}
