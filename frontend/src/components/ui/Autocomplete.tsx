import {
  Combobox,
  ComboboxButton,
  ComboboxInput,
  ComboboxOption,
  ComboboxOptions,
} from '@headlessui/react'
import { useState, type UIEvent } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { catalog } from '../../api/endpoints'
import type { Suggestion } from '../../api/types'
import { useDebounced } from '../../lib/useDebounced'
import { Icon } from './Icon'
import styles from './menu.module.css'
import inputStyles from './controls.module.css'

const PAGE_SIZE = 30

interface AutocompleteProps {
  kind: 'genre' | 'artist'
  value: string
  onChange: (value: string) => void
  placeholder: string
  ariaLabel: string
}

export function Autocomplete({ kind, value, onChange, placeholder, ariaLabel }: AutocompleteProps) {
  const [query, setQuery] = useState('')
  const debounced = useDebounced(query, 180)
  const term = debounced.trim()

  const suggestions = useInfiniteQuery({
    queryKey: ['suggest', kind, term],
    queryFn: ({ pageParam }) =>
      kind === 'genre'
        ? catalog.genres(term, PAGE_SIZE, pageParam)
        : catalog.artists(term, PAGE_SIZE, pageParam),
    initialPageParam: 0,
    getNextPageParam: (lastPage, pages) =>
      lastPage.length < PAGE_SIZE ? undefined : pages.length * PAGE_SIZE,
    enabled: kind === 'genre' || term.length > 0,
    staleTime: 60_000,
    placeholderData: (previous) => previous,
  })

  const options: Suggestion[] = suggestions.data?.pages.flat() ?? []
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = suggestions

  // Load the next page as the menu nears its end, whether by wheel, drag or
  // arrowing down through the options (which scrolls the menu too).
  function onMenuScroll(event: UIEvent<HTMLElement>) {
    const menu = event.currentTarget
    const nearEnd = menu.scrollTop + menu.clientHeight >= menu.scrollHeight - 80
    if (nearEnd && hasNextPage && !isFetchingNextPage) {
      void fetchNextPage()
    }
  }

  return (
    <Combobox
      value={value}
      onChange={(next) => next !== null && onChange(next)}
      onClose={() => setQuery('')}
      immediate
    >
      <div className={styles.comboWrap}>
        <ComboboxInput
          className={inputStyles.control}
          aria-label={ariaLabel}
          placeholder={placeholder}
          displayValue={(current: string) => current}
          onChange={(event) => {
            setQuery(event.target.value)
            onChange(event.target.value)
          }}
        />
        <ComboboxButton className={styles.comboToggle} aria-label={`Show ${kind} suggestions`}>
          <Icon name="chevron-down" size={15} />
        </ComboboxButton>
      </div>
      <ComboboxOptions
        className={styles.menu}
        anchor="bottom start"
        transition
        onScroll={onMenuScroll}
      >
        {options.length === 0 ? (
          <div className={styles.empty}>
            {kind === 'artist' && term.length === 0 ? 'Type an artist name' : 'No matches'}
          </div>
        ) : (
          <>
            {options.map((option) => (
              <ComboboxOption key={option.mbid} value={option.name} className={styles.option}>
                <span>{option.name}</span>
              </ComboboxOption>
            ))}
            {hasNextPage ? (
              <div className={styles.listEnd} aria-live="polite">
                {isFetchingNextPage ? 'Loading more…' : ' '}
              </div>
            ) : options.length > PAGE_SIZE ? (
              <div className={styles.listEnd}>
                {options.length} {kind === 'genre' ? 'genres' : 'artists'}
              </div>
            ) : null}
          </>
        )}
      </ComboboxOptions>
    </Combobox>
  )
}
