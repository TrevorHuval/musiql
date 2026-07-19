export interface HealthReport {
  service: string
  databaseConnected: boolean
  status: string
}

export async function fetchHealth(): Promise<HealthReport> {
  const response = await fetch('/api/health')
  if (response.status !== 200 && response.status !== 503) {
    throw new Error(`Health request failed (${response.status})`)
  }
  return (await response.json()) as HealthReport
}
