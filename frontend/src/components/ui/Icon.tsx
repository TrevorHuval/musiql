import type { ReactElement, SVGProps } from 'react'

export type IconName =
  | 'plus'
  | 'trash'
  | 'pencil'
  | 'x'
  | 'check'
  | 'chevron-down'
  | 'chevron-right'
  | 'search'
  | 'library'
  | 'wave'
  | 'sliders'
  | 'code'
  | 'arrow-up'
  | 'arrow-down'
  | 'sun'
  | 'moon'
  | 'logout'
  | 'grip'
  | 'disc'
  | 'spark'
  | 'settings'
  | 'link'
  | 'refresh'
  | 'external'

const paths: Record<IconName, ReactElement> = {
  plus: <path d="M12 5v14M5 12h14" />,
  trash: <path d="M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3" />,
  pencil: <path d="M4 20h4L18.5 9.5a2.1 2.1 0 0 0-3-3L5 17v3M13.5 6.5l3 3" />,
  x: <path d="M6 6l12 12M18 6L6 18" />,
  check: <path d="M5 12.5l4.5 4.5L19 7" />,
  'chevron-down': <path d="M6 9l6 6 6-6" />,
  'chevron-right': <path d="M9 6l6 6-6 6" />,
  search: <path d="M11 19a8 8 0 1 1 0-16 8 8 0 0 1 0 16zM21 21l-4.3-4.3" />,
  library: <path d="M4 5v14M9 5v14M14 6l4.5 12.5M4 5h5M9 5h4l5 14" />,
  wave: <path d="M3 12h2l2-6 3 14 3-18 3 12 2-4h3" />,
  sliders: (
    <path d="M4 8h9M17 8h3M4 16h3M11 16h9M15 6v4M8 14v4" />
  ),
  code: <path d="M9 8l-5 4 5 4M15 8l5 4-5 4" />,
  'arrow-up': <path d="M12 19V5M6 11l6-6 6 6" />,
  'arrow-down': <path d="M12 5v14M6 13l6 6 6-6" />,
  sun: <path d="M12 4V2M12 22v-2M4 12H2M22 12h-2M5.6 5.6L4.2 4.2M19.8 19.8l-1.4-1.4M18.4 5.6l1.4-1.4M4.2 19.8l1.4-1.4M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z" />,
  moon: <path d="M20 14.5A8 8 0 0 1 9.5 4 7 7 0 1 0 20 14.5z" />,
  logout: <path d="M14 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2v-2M9 12h11M17 9l3 3-3 3" />,
  grip: <path d="M9 6h.01M9 12h.01M9 18h.01M15 6h.01M15 12h.01M15 18h.01" />,
  disc: <path d="M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2z" />,
  spark: <path d="M12 3l1.8 5.4L19 10l-5.2 1.6L12 17l-1.8-5.4L5 10l5.2-1.6L12 3z" />,
  settings: (
    <>
      <path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" />
      <path d="M19.4 13.5a1.6 1.6 0 0 0 .32 1.77l.05.05a2 2 0 1 1-2.83 2.83l-.05-.05a1.6 1.6 0 0 0-2.72 1.13V20a2 2 0 1 1-4 0v-.09a1.6 1.6 0 0 0-2.72-1.13l-.05.05a2 2 0 1 1-2.83-2.83l.05-.05A1.6 1.6 0 0 0 4.6 13.5H4a2 2 0 1 1 0-4h.09A1.6 1.6 0 0 0 5.22 6.8l-.05-.05a2 2 0 1 1 2.83-2.83l.05.05A1.6 1.6 0 0 0 10 4.6V4a2 2 0 1 1 4 0v.09a1.6 1.6 0 0 0 2.72 1.13l.05-.05a2 2 0 1 1 2.83 2.83l-.05.05A1.6 1.6 0 0 0 19.4 10.5H20a2 2 0 1 1 0 4z" />
    </>
  ),
  link: (
    <path d="M10 13a5 5 0 0 0 7 0l2-2a5 5 0 0 0-7-7l-1 1M14 11a5 5 0 0 0-7 0l-2 2a5 5 0 0 0 7 7l1-1" />
  ),
  refresh: (
    <path d="M4 12a8 8 0 0 1 13.7-5.7L20 8M20 4v4h-4M20 12a8 8 0 0 1-13.7 5.7L4 16M4 20v-4h4" />
  ),
  external: (
    <path d="M14 5h5v5M19 5l-8 8M12 5H7a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-5" />
  ),
}

interface IconProps extends SVGProps<SVGSVGElement> {
  name: IconName
  size?: number
}

export function Icon({ name, size = 16, ...rest }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...rest}
    >
      {paths[name]}
    </svg>
  )
}
