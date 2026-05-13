# Security Policy

## Supported Versions

Only the latest released version on `main` receives security updates.

## Reporting a Vulnerability

Please email security reports to **jakubvonsyrek@gmail.com**.  Do **not** open a public
GitHub issue for security vulnerabilities — they will be triaged in private and a fix
released before the issue is disclosed.

Include in your report:

- A description of the issue
- Steps to reproduce
- Affected version / commit SHA
- Potential impact

You should receive an acknowledgement within 72 hours.

## Best Practices for Users

- Do not expose `Pollmaster.Api` directly to the public internet without a reverse proxy
  enforcing TLS and rate limits.
- Set `Cors:AllowedOrigins` to your client origin(s) instead of `*` in production.
- Pin a specific upstream `Gios:BaseAddress` and a sensible `TimeoutSeconds`.
- Run the backend as an unprivileged user.

## Best Practices for Contributors

- Never commit secrets, credentials or `.env` files. The `.gitignore` excludes them, but
  always double-check `git diff` before pushing.
- Every public method has XML documentation.
- Validate input at the API boundary; trust internal callers.
- Use the `Result<T>` pattern for expected control flow; reserve exceptions for truly
  exceptional conditions.

## Security Measures in Code

- Strongly-typed options for every external dependency (`GiosOptions`, `CorsOptions`,
  `ApiClientOptions`) — no string-key magic at runtime.
- `Microsoft.Extensions.Http.Resilience` standard handler on every outbound HTTP call
  (timeouts, retry with backoff, circuit breaker).
- Configurable CORS policy; defaults to permissive in development only.
- No reflection-based deserialization paths exposed to user input.

## Known Vulnerabilities

None at this time.

## Deployment Checklist

- [ ] TLS termination in front of `Pollmaster.Api`
- [ ] `Cors:AllowedOrigins` restricted to known clients
- [ ] Application logs shipped to a centralized sink
- [ ] Health endpoint `/healthz` wired to platform liveness probe
- [ ] Backend running as a non-root user
- [ ] Backups (if any persistent storage is added later)
