import { forwardRef, type InputHTMLAttributes } from 'react'
import styles from './controls.module.css'

interface TextInputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean
}

export const TextInput = forwardRef<HTMLInputElement, TextInputProps>(function TextInput(
  { invalid, className, ...rest },
  ref,
) {
  return (
    <input
      ref={ref}
      className={[styles.control, className].filter(Boolean).join(' ')}
      data-invalid={invalid || undefined}
      {...rest}
    />
  )
})
