# Only same-origin scripts run and nothing may frame the app. Inline styles
# stay allowed because the UI sets element styles at runtime. HSTS, nosniff
# and the referrer policy are set by the edge proxy, so they are not repeated.
add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'" always;
add_header X-Frame-Options "DENY" always;
