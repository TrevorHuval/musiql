import { Icon, type IconName } from './Icon'
import styles from './Segmented.module.css'

export interface SegmentOption<T extends string> {
  value: T
  label: string
  icon?: IconName
}

interface SegmentedProps<T extends string> {
  value: T
  options: SegmentOption<T>[]
  onChange: (value: T) => void
  ariaLabel: string
  size?: 'sm' | 'md'
}

export function Segmented<T extends string>({
  value,
  options,
  onChange,
  ariaLabel,
  size = 'md',
}: SegmentedProps<T>) {
  return (
    <div className={styles.group} role="tablist" aria-label={ariaLabel} data-size={size}>
      {options.map((option) => (
        <button
          key={option.value}
          role="tab"
          aria-selected={option.value === value}
          className={styles.segment}
          data-active={option.value === value || undefined}
          onClick={() => onChange(option.value)}
        >
          {option.icon && <Icon name={option.icon} size={15} />}
          {option.label}
        </button>
      ))}
    </div>
  )
}
