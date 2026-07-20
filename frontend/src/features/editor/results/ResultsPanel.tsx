import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { QueryPage, ResultColumn } from '../../../api/types'
import { Button } from '../../../components/ui/Button'
import { Callout } from '../../../components/ui/Callout'
import { Icon, type IconName } from '../../../components/ui/Icon'
import { Spinner } from '../../../components/ui/misc'
import { formatDuration } from '../../../lib/format'
import styles from './results.module.css'

export type ResultsView =
  | { kind: 'empty-query' }
  | { kind: 'invalid' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'library-empty' }
  | { kind: 'no-results' }
  | { kind: 'results'; page: QueryPage }

interface ResultsPanelProps {
  view: ResultsView
  entityLabel: string
  busy?: boolean
  onPageChange: (page: number) => void
}

export function ResultsPanel({ view, entityLabel, busy, onPageChange }: ResultsPanelProps) {
  return (
    <section className={styles.panel}>
      <header className={styles.head}>
        <div className={styles.headLeft}>
          <span className={styles.headTitle}>Results</span>
          {view.kind === 'results' && (
            <span className={styles.count}>
              {view.page.total} {view.page.total === 1 ? 'row' : 'rows'}
            </span>
          )}
          {(view.kind === 'loading' || (busy && view.kind === 'results')) && (
            <span className={styles.updating}>running…</span>
          )}
        </div>
        {view.kind === 'results' && view.page.total > view.page.pageSize && (
          <Pager page={view.page} onPageChange={onPageChange} />
        )}
      </header>

      <div className={styles.body}>
        <Content view={view} entityLabel={entityLabel} />
      </div>
    </section>
  )
}

function Content({ view, entityLabel }: { view: ResultsView; entityLabel: string }) {
  switch (view.kind) {
    case 'empty-query':
      return (
        <Placeholder icon="wave" title="Nothing to play yet">
          Add a filter or type a query and the matching {entityLabel.toLowerCase()} appear here.
        </Placeholder>
      )
    case 'invalid':
      return (
        <Placeholder icon="code" title="Waiting on a valid query">
          Fix the highlighted issues and the results refresh on their own.
        </Placeholder>
      )
    case 'loading':
      return (
        <div className={styles.centered}>
          <Spinner />
        </div>
      )
    case 'error':
      return (
        <div className={styles.calloutWrap}>
          <Callout tone="clay" icon="x" title="Couldn’t run the query">
            {view.message}
          </Callout>
        </div>
      )
    case 'library-empty':
      return (
        <div className={styles.calloutWrap}>
          <Callout
            tone="signal"
            icon="library"
            title="Your library is empty"
            action={
              <Link to="/settings" className={styles.connectLink}>
                <Icon name="link" size={14} />
                Connect Spotify
              </Link>
            }
          >
            This query is scoped to your library, but nothing’s synced yet. Connect Spotify and sync
            your saved tracks to fill it, or switch the source to “All music”.
          </Callout>
        </div>
      )
    case 'no-results':
      return (
        <Placeholder icon="search" title="No matches">
          No {entityLabel.toLowerCase()} fit these filters. Try loosening a value or widening the
          year range.
        </Placeholder>
      )
    case 'results':
      return <ResultsTable page={view.page} />
  }
}

function ResultsTable({ page }: { page: QueryPage }) {
  const visible = page.columns
    .map((column, index) => ({ column, index }))
    .filter(({ column }) => column.type !== 'guid')

  return (
    <div className={styles.tableScroll}>
      <table className={styles.table}>
        <thead>
          <tr>
            {visible.map(({ column }) => (
              <th key={column.name} className={isNumeric(column) ? styles.numCol : undefined}>
                {columnLabel(column.name)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {page.rows.map((row, rowIndex) => (
            <tr key={rowIndex}>
              {visible.map(({ column, index }) => (
                <td key={column.name} className={isNumeric(column) ? styles.numCell : undefined}>
                  {formatCell(row[index], column)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Pager({ page, onPageChange }: { page: QueryPage; onPageChange: (page: number) => void }) {
  const from = (page.page - 1) * page.pageSize + 1
  const to = Math.min(page.page * page.pageSize, page.total)
  return (
    <div className={styles.pager}>
      <span className={styles.pagerRange}>
        {from}–{to} of {page.total}
      </span>
      <Button size="sm" variant="ghost" disabled={page.page <= 1} onClick={() => onPageChange(page.page - 1)}>
        <Icon name="chevron-right" size={15} className={styles.flip} />
      </Button>
      <Button size="sm" variant="ghost" disabled={!page.hasMore} onClick={() => onPageChange(page.page + 1)}>
        <Icon name="chevron-right" size={15} />
      </Button>
    </div>
  )
}

function Placeholder({
  icon,
  title,
  children,
}: {
  icon: IconName
  title: string
  children: ReactNode
}) {
  return (
    <div className={styles.placeholder}>
      <span className={styles.placeholderMark} aria-hidden="true">
        <Icon name={icon} size={22} />
      </span>
      <p className={styles.placeholderTitle}>{title}</p>
      <p className={styles.placeholderText}>{children}</p>
    </div>
  )
}

function isNumeric(column: ResultColumn): boolean {
  return column.type === 'number'
}

function columnLabel(name: string): string {
  if (name === 'length_ms') return 'Length'
  return name.charAt(0).toUpperCase() + name.slice(1)
}

function formatCell(value: string | number | null, column: ResultColumn): string {
  if (value == null) return '—'
  if (column.name === 'length_ms' && typeof value === 'number') return formatDuration(value)
  return String(value)
}
