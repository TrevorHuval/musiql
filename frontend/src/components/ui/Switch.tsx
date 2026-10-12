import { Switch as HeadlessSwitch } from '@headlessui/react'
import styles from './Switch.module.css'

interface SwitchProps {
  checked: boolean
  onChange: (checked: boolean) => void
  label: string
  disabled?: boolean
}

export function Switch({ checked, onChange, label, disabled }: SwitchProps) {
  return (
    <HeadlessSwitch
      checked={checked}
      onChange={onChange}
      disabled={disabled}
      aria-label={label}
      className={styles.track}
    >
      <span className={styles.thumb} />
    </HeadlessSwitch>
  )
}
