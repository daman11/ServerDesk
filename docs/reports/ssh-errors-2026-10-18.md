# SSH connection failure diagnostics

Development after 0.42.0; 2026-10-18.

Authentication rejection previously shared a brief generic message. SSH.NET now retains the selected authentication method and known advertised method names in a typed authentication exception. The UI distinguishes password/key rejection, explains the ambiguity of the SSH authentication response, and directs the user to server logs for the exact account policy reason. It never infers a wrong password, locked account or access ban without a specific protocol code.

Local key loading failures, DNS, socket refusal/routing/reset, timeouts and known SSH disconnect codes have separate Polish/English guidance. The session header keeps the failure heading with full details in its tooltip; the ordinary error dialog uses the same formatter. Diagnostics retain outer/inner stacks, socket/disconnect codes and the key failure category, excluding arbitrary exception messages/data. Host trust, authentication policies, retry and cancellation behavior are unchanged.

Validation:

- Focused Windows tests: **67 passed**, covering SSH messages/factory fixtures, operation errors, SFTP error mapping and localization.
- Application/UI smoke build: **0 warnings, 0 errors**.
- `--ssh-errors`: **passed**, Polish/English real missing-key and refused-loopback failures, repeated failure handling, inline heading/full tooltip and no failed-attempt history. Only disposable data and bound non-listening loopback sockets were used.
- Evidence: `artifacts/reports/ssh-errors-2026-10-18/`, including TRX and four rendered frames.

No real-server credential attempts were made. Server-side account policy failures are covered by protocol-code fixtures; the server can deliberately return an opaque authentication failure.
