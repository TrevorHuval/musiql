import type { ReactNode } from 'react'
import { Disclosure, DisclosureButton, DisclosurePanel } from '@headlessui/react'
import { Icon } from '../../../components/ui/Icon'
import styles from './advanced.module.css'

interface CheatSheetProps {
  onInsert: (mql: string) => void
}

const FIELDS: Array<[string, string]> = [
  ['artist', 'credited artist name (text)'],
  ['album / title', 'release or track title (text)'],
  ['track / name', 'recording title (text)'],
  ['type', 'album primary type — Album, EP… (text)'],
  ['genre', 'membership test across recording, album, artist'],
  ['year', 'first release year; artist begin year (number)'],
  ['decade', 'year floored to a decade, e.g. 1990'],
  ['length', 'recording length in seconds (number)'],
  ['votes / rating', 'strongest genre-tag vote — use with order by'],
]

const OPERATORS: Array<[string, string]> = [
  ['= != < <= > >=', 'compare; text and numbers'],
  ['in (a, b, …)', 'any of a list'],
  ['between x and y', 'inclusive numeric range'],
  ['contains "text"', 'case-insensitive substring'],
  ['and · or · not · ( )', 'combine and group; not binds tightest'],
]

const EXAMPLES = [
  'tracks where genre = "grunge" and year between 1990 and 2004 and artist != "Nirvana"',
  'albums where genre in ("grunge", "alternative rock") and year >= 1990 order by rating desc',
  'tracks where artist = "Pearl Jam" and length between 180 and 300 order by year',
  'tracks where (genre = "punk" or genre = "hardcore") and not artist contains "tribute"',
]

export function CheatSheet({ onInsert }: CheatSheetProps) {
  return (
    <aside className={styles.cheat}>
      <Section title="Shape" defaultOpen>
        <pre className={styles.cheatShape}>
          {'<entity> [from library]\n[where <expr>]\n[order by <key> …]\n[limit <n>]'}
        </pre>
        <p className={styles.cheatNote}>
          Entities: <code>tracks</code>, <code>albums</code>, <code>artists</code>. Clauses appear
          in that order.
        </p>
      </Section>

      <Section title="Fields">
        <dl className={styles.cheatList}>
          {FIELDS.map(([name, desc]) => (
            <div key={name} className={styles.cheatItem}>
              <dt>{name}</dt>
              <dd>{desc}</dd>
            </div>
          ))}
        </dl>
      </Section>

      <Section title="Operators">
        <dl className={styles.cheatList}>
          {OPERATORS.map(([name, desc]) => (
            <div key={name} className={styles.cheatItem}>
              <dt>{name}</dt>
              <dd>{desc}</dd>
            </div>
          ))}
        </dl>
      </Section>

      <Section title="Genre">
        <p className={styles.cheatNote}>
          An item matches a genre if any of its levels carries it — a track counts if its recording,
          album, or artist is tagged. Text comparisons are case-insensitive.
        </p>
      </Section>

      <Section title="Examples" defaultOpen>
        <div className={styles.cheatExamples}>
          {EXAMPLES.map((example) => (
            <button key={example} type="button" className={styles.cheatExample} onClick={() => onInsert(example)}>
              <code>{example}</code>
              <span className={styles.cheatExampleAdd} aria-hidden="true">
                <Icon name="arrow-up" size={13} />
              </span>
            </button>
          ))}
        </div>
      </Section>
    </aside>
  )
}

function Section({
  title,
  children,
  defaultOpen = false,
}: {
  title: string
  children: ReactNode
  defaultOpen?: boolean
}) {
  return (
    <Disclosure defaultOpen={defaultOpen}>
      <DisclosureButton className={styles.cheatHead}>
        <span>{title}</span>
        <Icon name="chevron-down" size={15} className={styles.cheatChevron} />
      </DisclosureButton>
      <DisclosurePanel className={styles.cheatPanel}>{children}</DisclosurePanel>
    </Disclosure>
  )
}
