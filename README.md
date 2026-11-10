# ERP assignment modules

This .NET 10 solution contains two independent ERP assignment modules. Each has separate Domain, Application, Infrastructure and Api projects, SQL Server 2022 scripts, executable APIs and automated tests.

Start with [SUBMISSION.md](SUBMISSION.md) for prerequisites, database provisioning, connection strings, startup commands and verification.

| Module | Guide and walkthrough | Design and ER diagram | Standalone ER diagram |
|---|---|---|---|
| Discounts | [Guide](docs/discounts/README.md) | [Design](docs/discounts/design.md) | [SVG](docs/discounts/er-diagram.svg) |
| Customer custom fields | [Guide](docs/customer-custom-fields/README.md) | [Design](docs/customer-custom-fields/design.md) | [SVG](docs/customer-custom-fields/er-diagram.svg) |

Discounts calculates an ordered pricing breakdown using price lists, promotions and coupons. Customer custom fields provides global text/multi-select definitions, per-customer values, optimistic concurrency, temporal change history and a demo page. The ER diagrams show database entities, keys and relationships; design notes distinguish real foreign keys from external identifiers and conceptual temporal history.

```text
ERP.sln
src/
  Discounts/            Domain, Application, Infrastructure, Api
  CustomerCustomFields/ Domain, Application, Infrastructure, Api
  Shared/               ERP.Infrastructure: CacheEntry<T>
tests/
  Discounts/
  CustomerCustomFields/
sql/
  discounts/
  customer-custom-fields/
docs/
  discounts/            README.md, design.md, er-diagram.svg
  customer-custom-fields/ README.md, design.md, er-diagram.svg
```

Both modules use SQL Server 2022 (16.x), compatibility 160, with separate databases and stored-procedure Dapper access. Database enums are numeric. Customer identifiers are regular application-generated GUIDs stored as uniqueidentifier; no external ID generator is used. Schema scripts require fresh databases; no EF, triggers or migrations are used.

Both Infrastructure projects reference the small shared CacheEntry<T> component. Repository decorators own catalog snapshots and invalidation policy; business models and rules remain separate. The custom-field demonstration does not feed discount eligibility. SQL and API samples are in [samples/pricing.http](samples/pricing.http) and [samples/customer-custom-fields.http](samples/customer-custom-fields.http).

The supplied specifications under docs/superpowers/specs are historical references; current module guides and design documents incorporate the explicitly agreed changes. [Verification evidence](docs/verification.md) records build, test and review results.
