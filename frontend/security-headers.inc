# Only same-origin scripts run and nothing may frame the app. Inline styles
# stay allowed because the UI sets element styles at runtime. HSTS, nosniff
# and the referrer policy are set by the edge proxy, so they are not repeated.
# Google Analytics 4 needs googletagmanager.com (gtag.js) and the google-analytics.com /
# analytics.google.com collection endpoints; its init snippet is a same-origin file.
add_header Content-Security-Policy "default-src 'self'; script-src 'self' https://www.googletagmanager.com; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://*.google-analytics.com https://*.googletagmanager.com; font-src 'self'; connect-src 'self' https://*.google-analytics.com https://*.analytics.google.com https://*.googletagmanager.com; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'" always;
add_header X-Frame-Options "DENY" always;
