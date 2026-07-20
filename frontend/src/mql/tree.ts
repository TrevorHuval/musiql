import type { BuilderNode, Condition, Group } from './types'

export function updateCondition(
  group: Group,
  id: string,
  patch: Partial<Condition>,
): Group {
  return {
    ...group,
    children: group.children.map((child) => {
      if (child.kind === 'group') return updateCondition(child, id, patch)
      return child.id === id ? { ...child, ...patch } : child
    }),
  }
}

export function updateGroup(group: Group, id: string, patch: Partial<Group>): Group {
  const next = group.id === id ? { ...group, ...patch } : group
  return {
    ...next,
    children: next.children.map((child) =>
      child.kind === 'group' ? updateGroup(child, id, patch) : child,
    ),
  }
}

export function addToGroup(group: Group, groupId: string, node: BuilderNode): Group {
  if (group.id === groupId) {
    return { ...group, children: [...group.children, node] }
  }
  return {
    ...group,
    children: group.children.map((child) =>
      child.kind === 'group' ? addToGroup(child, groupId, node) : child,
    ),
  }
}

export function removeNode(group: Group, id: string): Group {
  return {
    ...group,
    children: group.children
      .filter((child) => child.id !== id)
      .map((child) => (child.kind === 'group' ? removeNode(child, id) : child)),
  }
}

export function countConditions(group: Group): number {
  return group.children.reduce(
    (total, child) => total + (child.kind === 'group' ? countConditions(child) : 1),
    0,
  )
}
