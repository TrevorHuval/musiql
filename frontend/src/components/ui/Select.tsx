import {
  Listbox,
  ListboxButton,
  ListboxOption,
  ListboxOptions,
} from '@headlessui/react'
import { Icon } from './Icon'
import styles from './menu.module.css'

export interface SelectOption<T extends string> {
  value: T
  label: string
}

interface SelectProps<T extends string> {
  value: T
  options: SelectOption<T>[]
  onChange: (value: T) => void
  ariaLabel: string
  size?: 'sm' | 'md'
}

export function Select<T extends string>({
  value,
  options,
  onChange,
  ariaLabel,
  size = 'md',
}: SelectProps<T>) {
  const active = options.find((option) => option.value === value)
  return (
    <Listbox value={value} onChange={onChange}>
      <ListboxButton className={styles.trigger} aria-label={ariaLabel} data-size={size}>
        <span className={styles.triggerLabel}>{active?.label ?? value}</span>
        <Icon name="chevron-down" size={15} className={styles.triggerIcon} />
      </ListboxButton>
      <ListboxOptions anchor="bottom start" className={styles.menu} transition>
        {options.map((option) => (
          <ListboxOption key={option.value} value={option.value} className={styles.option}>
            <span>{option.label}</span>
            {option.value === value && <Icon name="check" size={15} />}
          </ListboxOption>
        ))}
      </ListboxOptions>
    </Listbox>
  )
}
