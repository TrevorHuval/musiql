import { render, screen } from '@testing-library/react'
import { HealthBadge } from './HealthBadge'

describe('HealthBadge', () => {
  it('reports a healthy database', () => {
    render(
      <HealthBadge
        view={{
          status: 'ready',
          report: { service: 'musiql-api', databaseConnected: true, status: 'healthy' },
        }}
      />,
    )

    expect(screen.getByText('API and database healthy')).toBeInTheDocument()
  })

  it('reports an unreachable API', () => {
    render(<HealthBadge view={{ status: 'error' }} />)

    expect(screen.getByText('API unreachable')).toBeInTheDocument()
  })
})
