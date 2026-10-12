import { useTheme } from '../../app/ThemeProvider'
import { Icon } from '../ui/Icon'

export function ThemeToggle() {
  const { theme, toggle } = useTheme()
  const label = theme === 'dark' ? 'Use light theme' : 'Use dark theme'

  return (
    <button type="button" className="icon-button" aria-label={label} title={label} onClick={toggle}>
      <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={16} />
    </button>
  )
}
