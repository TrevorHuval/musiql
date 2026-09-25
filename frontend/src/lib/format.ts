export function formatDuration(ms: number | null): string {
  if (ms == null) return '—'
  const totalSeconds = Math.round(ms / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${seconds.toString().padStart(2, '0')}`
}

export function formatDate(iso: string): string {
  const date = new Date(iso)
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}

export function formatRelative(iso: string): string {
  const then = new Date(iso).getTime()
  const diff = Date.now() - then
  const minute = 60_000
  const hour = 60 * minute
  const day = 24 * hour

  if (diff < minute) return 'just now'
  if (diff < hour) return `${Math.floor(diff / minute)}m ago`
  if (diff < day) return `${Math.floor(diff / hour)}h ago`
  if (diff < 7 * day) return `${Math.floor(diff / day)}d ago`
  return formatDate(iso)
}

export function formatUntil(iso: string): string {
  const diff = new Date(iso).getTime() - Date.now()
  const hour = 3_600_000
  if (diff <= 0) return 'within the hour'
  if (diff < hour) return `in ${Math.max(1, Math.round(diff / 60_000))}m`
  if (diff < 48 * hour) return `in ${Math.round(diff / hour)}h`
  return `in ${Math.round(diff / (24 * hour))}d`
}
