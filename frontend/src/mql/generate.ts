import type { Operator } from '../api/types'
import type { BuilderNode, BuilderState, Condition, FieldTypeResolver, Group } from './types'

const NUMERIC_OPS: Operator[] = ['<', '<=', '>', '>=', 'between']

function escapeString(raw: string): string {
  return raw
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\t/g, '\\t')
    .replace(/\r/g, '\\r')
}

function quote(raw: string): string {
  return `"${escapeString(raw)}"`
}

function isNumericField(field: string, op: Operator, resolveType: FieldTypeResolver): boolean {
  if (resolveType(field) === 'number') return true
  return resolveType(field) === undefined && NUMERIC_OPS.includes(op)
}

function renderValue(field: string, op: Operator, raw: string, resolveType: FieldTypeResolver): string {
  return isNumericField(field, op, resolveType) ? raw.trim() : quote(raw.trim())
}

export function isConditionComplete(condition: Condition): boolean {
  if (!condition.field) return false
  switch (condition.op) {
    case 'in':
      return condition.values.some((value) => value.trim() !== '')
    case 'between':
      return condition.value.trim() !== '' && condition.valueHigh.trim() !== ''
    default:
      return condition.value.trim() !== ''
  }
}

function renderCondition(condition: Condition, resolveType: FieldTypeResolver): string {
  const { field, op } = condition
  switch (op) {
    case 'in': {
      const values = condition.values
        .map((value) => value.trim())
        .filter(Boolean)
        .map((value) => renderValue(field, op, value, resolveType))
      return `${field} in (${values.join(', ')})`
    }
    case 'between': {
      const low = renderValue(field, op, condition.value, resolveType)
      const high = renderValue(field, op, condition.valueHigh, resolveType)
      return `${field} between ${low} and ${high}`
    }
    case 'contains':
      return `${field} contains ${renderValue(field, op, condition.value, resolveType)}`
    default:
      return `${field} ${op} ${renderValue(field, op, condition.value, resolveType)}`
  }
}

function renderNode(node: BuilderNode, resolveType: FieldTypeResolver): string | null {
  if (node.kind === 'condition') {
    return isConditionComplete(node) ? renderCondition(node, resolveType) : null
  }
  return renderGroup(node, resolveType, false)
}

function renderGroup(group: Group, resolveType: FieldTypeResolver, isRoot: boolean): string | null {
  const parts = group.children
    .map((child) => renderNode(child, resolveType))
    .filter((part): part is string => part !== null)

  if (parts.length === 0) return null

  const joined = parts.join(` ${group.combinator} `)
  if (isRoot || parts.length === 1) return joined
  return `(${joined})`
}

export function generateMql(state: BuilderState, resolveType: FieldTypeResolver): string {
  const segments: string[] = [state.entity]

  if (state.fromLibrary) segments.push('from library')

  const where = renderGroup(state.root, resolveType, true)
  if (where) segments.push(`where ${where}`)

  const sortKeys = state.sort.filter((key) => key.field)
  if (sortKeys.length > 0) {
    const keys = sortKeys.map((key) => (key.direction === 'desc' ? `${key.field} desc` : key.field))
    segments.push(`order by ${keys.join(', ')}`)
  }

  if (state.limit != null) segments.push(`limit ${state.limit}`)

  return segments.join(' ')
}
