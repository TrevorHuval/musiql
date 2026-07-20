import {
  Combobox,
  ComboboxButton,
  ComboboxInput,
  ComboboxOption,
  ComboboxOptions,
} from '@headlessui/react'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { catalog } from '../../api/endpoints'
import type { Suggestion } from '../../api/types'
import { useDebounced } from '../../lib/useDebounced'
import { Icon } from './Icon'
import styles from './menu.module.css'
import inputStyles from './controls.module.css'

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

  const suggestions = useQuery({
    queryKey: ['suggest', kind, debounced],
    queryFn: () =>
      kind === 'genre' ? catalog.genres(debounced, 12) : catalog.artists(debounced, 12),
    enabled: kind === 'genre' || debounced.trim().length > 0,
    staleTime: 60_000,
    placeholderData: (previous) => previous,
  })

  const options: Suggestion[] = suggestions.data ?? []

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
      <ComboboxOptions className={styles.menu} anchor="bottom start" transition>
        {options.length === 0 ? (
          <div className={styles.empty}>
            {kind === 'artist' && debounced.trim().length === 0
              ? 'Type an artist name'
              : 'No matches'}
          </div>
        ) : (
          options.map((option) => (
            <ComboboxOption key={option.mbid} value={option.name} className={styles.option}>
              <span>{option.name}</span>
            </ComboboxOption>
          ))
        )}
      </ComboboxOptions>
    </Combobox>
  )
}
