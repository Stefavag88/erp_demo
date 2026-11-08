# Bounded retries for SQL reads

The user approved bounded database retries as a separate commit after fixing provider registration. Result-pattern changes, circuit breakers, additional caching and schema migrations are not part of this change.

Infrastructure's SqlReadRetryPolicy uses Polly.Core 8.4.2. It executes an original attempt plus at most two retries, with 100ms exponential backoff, jitter and a 500ms maximum delay. An outer five-second cooperative timeout covers the complete read, including opening, execution, materialization and retry delays. Callback code must honor the supplied token; disposal/driver cancellation can take additional time, so this is not a hard real-time deadline.

Only SqlException primary error codes 1205 (deadlock), 10928/10929 (resource limits), 40197/40613 (service interruption) and 40501 (service busy) are retried. Authentication/authorization, missing schema/procedures, definition validation, command timeout (-2), other exceptions and caller cancellation are not retried. Unknown codes fail closed rather than guessing they are transient.

The three Dapper repositories execute one complete stored-procedure read inside the policy. Each attempt creates fresh parameters/TVP and opens/disposes a fresh SqlConnection object; connection pooling remains enabled. SqlConnectionFactory sets ConnectRetryCount=0 so configurable driver reconnection retries do not multiply the application policy. No SqlClient command retry provider is installed. The API registers one thread-safe singleton policy using the injected TimeProvider.

Retries capture the original repository inputs and AsOfUtc; the engine, category providers and calculator are not retried. Promotion cache hits bypass SQL as before; failed cache loads retry at the Dapper boundary and are not cached. Exhaustion/time-budget expiry remains a failure and never produces an unchanged/partial price.

Tests use fresh disposable SQL databases and real SqlException instances raised by test-only fault-injected procedures. They verify successful recovery, exhaustion, permanent errors, fresh/closed connections and unchanged evaluation inputs. No-SQL tests cover code classification, cancellation/non-SQL failures and the overall timeout with a fake TimeProvider.

References: [Polly retry](https://www.pollydocs.org/strategies/retry.html), [Polly cooperative timeout](https://www.pollydocs.org/strategies/timeout.html), [SqlException](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlexception?view=sqlclient-dotnet-7.1). The policy deliberately uses explicit codes rather than assuming every SqlException is transient.
