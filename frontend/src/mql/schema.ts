import type { Operator, SchemaEntity, SchemaResponse } from '../api/types'
import type { Entity, FieldTypeResolver } from './types'

export const OPERATOR_LABELS: Record<Operator, string> = {
  '=': 'is',
  '!=': 'is not',
  '<': 'less than',
  '<=': 'at most',
  '>': 'greater than',
  '>=': 'at least',
  in: 'is any of',
  between: 'between',
  contains: 'contains',
}

export const ENTITY_LABELS: Record<Entity, string> = {
  tracks: 'Tracks',
  albums: 'Albums',
  artists: 'Artists',
}

export function findEntity(schema: SchemaResponse, entity: Entity): SchemaEntity | undefined {
  return schema.entities.find((candidate) => candidate.name === entity)
}

export function resolverFor(entity: SchemaEntity | undefined): FieldTypeResolver {
  const byName = new Map<string, string>()
  if (entity) {
    for (const field of entity.fields) {
      byName.set(field.name, field.type)
      for (const alias of field.aliases) byName.set(alias, field.type)
    }
  }
  return (field: string) => byName.get(field) as ReturnType<FieldTypeResolver>
}

export function orderableFields(entity: SchemaEntity | undefined): string[] {
  if (!entity) return []
  return entity.fields.filter((field) => field.orderable).map((field) => field.name)
}
