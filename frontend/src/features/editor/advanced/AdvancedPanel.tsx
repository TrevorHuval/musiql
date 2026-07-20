import type { MqlError } from '../../../api/types'
import { Icon } from '../../../components/ui/Icon'
import { CheatSheet } from './CheatSheet'
import { MqlEditor } from './MqlEditor'
import styles from './advanced.module.css'

interface AdvancedPanelProps {
  value: string
  onChange: (value: string) => void
  errors: MqlError[]
}

export function AdvancedPanel({ value, onChange, errors }: AdvancedPanelProps) {
  return (
    <div className={styles.panel}>
      <div className={styles.main}>
        <MqlEditor value={value} onChange={onChange} errors={errors} />
        {errors.length > 0 && (
          <ul className={styles.errorList}>
            {errors.map((error, index) => (
              <li key={index} className={styles.errorItem}>
                <Icon name="x" size={14} className={styles.errorIcon} />
                <span className={styles.errorMessage}>{error.message}</span>
                <span className={styles.errorPos}>col {error.start + 1}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <CheatSheet onInsert={onChange} />
    </div>
  )
}
