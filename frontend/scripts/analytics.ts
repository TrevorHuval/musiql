import type { Plugin } from 'vite'

const INIT_FILE = 'ga-init.js'

/**
 * Google Analytics 4 (free tier), or nothing.
 *
 * The measurement ID is public, it ships in every visitor's page source, so it is build
 * config like `BASE_PATH` rather than a secret. Without `GA_MEASUREMENT_ID` (local dev,
 * tests, forks) no tag and no init file are emitted.
 *
 * The init code is a same-origin file instead of an inline script so the nginx CSP can
 * stay at `script-src 'self'` plus gtag.js. Do Not Track visitors never load gtag.js.
 * Google Signals and ad personalization are off, and `send_page_view` is off because
 * the SPA reports its own page views from `lib/analytics.ts`.
 */
export function analytics(): Plugin {
  let measurementId = ''
  let base = '/'

  return {
    name: 'musiql-analytics',

    configResolved(config) {
      base = config.base
      measurementId = (process.env.GA_MEASUREMENT_ID ?? '').trim()

      if (measurementId !== '' && !/^G-[A-Z0-9]{6,12}$/.test(measurementId)) {
        throw new Error(`GA_MEASUREMENT_ID must look like G-XXXXXXXXXX, got "${measurementId}"`)
      }
    },

    transformIndexHtml(html) {
      if (measurementId === '') return html
      return html.replace('</head>', `    <script src="${base}${INIT_FILE}"></script>\n  </head>`)
    },

    generateBundle() {
      if (measurementId === '') return
      this.emitFile({ type: 'asset', fileName: INIT_FILE, source: initScript(measurementId) })
    },
  }
}

function initScript(id: string): string {
  return `window.dataLayer = window.dataLayer || [];
function gtag() { dataLayer.push(arguments); }
if (navigator.doNotTrack !== '1' && window.doNotTrack !== '1') {
  var s = document.createElement('script');
  s.async = true;
  s.src = 'https://www.googletagmanager.com/gtag/js?id=${id}';
  document.head.appendChild(s);
  gtag('js', new Date());
  gtag('config', '${id}', {
    send_page_view: false,
    allow_google_signals: false,
    allow_ad_personalization_signals: false
  });
  window.gaEnabled = true;
}
`
}
