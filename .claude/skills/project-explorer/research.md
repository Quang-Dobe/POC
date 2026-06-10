# project-explorer research notes

These notes are the auditable source for the heuristics used by the `project-explorer` runtime agent. The skill (`SKILL.md` next to this file) cites this document by relative path. Do not link out to planning artifacts from here — this file must remain self-contained at runtime.

## Sources

**Evans, Eric — _Domain-Driven Design: Tackling Complexity in the Heart of Software_ (Addison-Wesley, 2003).**
The "Blue Book". Canonical source for the tactical vocabulary the agent emits (aggregate, repository, domain event, domain service, value object, bounded context, ubiquitous language, context map). The locked `docs/domain/` output shape mirrors Evans' tactical-pattern catalogue and the context-map idea. Cited here because every category the agent writes traces back to a chapter in this book.

**Vernon, Vaughn — _Implementing Domain-Driven Design_ (Addison-Wesley, 2013).**
The "IDDD" book. Operational follow-up to Evans, with concrete tactical patterns (aggregate design rules, small aggregates, eventual consistency between aggregates, references by identity). Cited here for the BC-discovery treatment as a sociotechnical activity (not pure code inference) — directly motivates printing the bounded-context decisions in the candidate report for the audit trail before auto-writing.

**Jimmy Bogard — jimmybogard.com (MediatR / vertical slice writings).**
Current .NET ecosystem source. Author of MediatR and the vertical-slice architecture style widely adopted in modern .NET codebases. Cited here for two reasons: (a) MediatR's `IRequest` / `IRequestHandler` / `INotification` shapes are the single most reliable command / event signals in .NET; (b) Bogard's commentary on the anemic domain model and on "favouring behaviour over data" feeds directly into the failure-mode catalogue below.

## DDD code signals

Concrete patterns the agent watches for during the repo walk. .NET signals are first-class; a trailing "Other stacks (best-effort)" bullet gives broad-stroke hints for non-.NET stacks.

### Aggregates

- Class inherits a base such as `AggregateRoot<TId>`, `Entity<TId>`, or implements an interface like `IAggregateRoot`.
- Constructor enforces invariants (argument validation, throws on bad state) and there is no parameterless public constructor — only a `private` / `protected` one for ORM materialization.
- Mutable state is exposed only through methods (behaviour-first); properties have `private set` / `init` and collections are surfaced as `IReadOnlyList<T>` / `IReadOnlyCollection<T>` while a private `List<T>` holds the backing store.
- The class raises domain events from inside its methods (e.g., `AddDomainEvent(new OrderPlaced(...))` against a base class).
- A single repository interface in the same folder takes only this class as `Add` / `Update` / `GetById` target — strong hint this is the aggregate root for that cluster.
- Other stacks (best-effort): TypeScript / Java look-alikes — classes named with the `Aggregate` suffix, files grouped under `domain/<aggregate>/`, ORM annotations such as `@Entity` paired with private setters and factory static methods.

### Repositories

- Interface name ends in `Repository` and lives next to the aggregate it serves (e.g., `Domain/Orders/IOrderRepository.cs`).
- Methods are aggregate-shaped: `GetById`, `Add`, `Update`, `Remove` returning the aggregate root, not arbitrary projections; query methods return `Task<TAggregate?>` or `Task<IReadOnlyList<TAggregate>>`.
- Implementation lives in `Infrastructure/` (or `Persistence/`) and depends on an ORM context (`DbContext`, `IMongoCollection<T>`, Dapper `IDbConnection`) — domain layer holds the interface only.
- Anti-signal that still counts as detection: a generic `IRepository<T>` used for everything. Flagged as a candidate but cross-referenced against the "Generic repository everywhere" failure mode in `## Known failure modes`.
- Other stacks (best-effort): Java Spring `@Repository` annotated interfaces; Node.js / TS classes named `*Repository` in a `repositories/` folder; Python files matching `*_repository.py` re-exporting CRUD-shaped functions.

### Events

- Class implements an interface such as `IDomainEvent` / `INotification` (MediatR), or inherits `DomainEvent` / `IntegrationEvent`.
- Class name is past-tense and noun-phrase: `OrderPlaced`, `PaymentCaptured`, `InvoiceVoided` — past-tense verb is the strongest single naming signal.
- The class is immutable: all properties are `get`-only / `init`, the constructor sets every field, and there are no mutator methods.
- Raised from inside an aggregate method (look for `AddDomainEvent(...)` / `_events.Add(...)` call sites) — pairs the event back to its emitting aggregate for the `events.md` table row.
- Other stacks (best-effort): TS files exporting types whose names end in `Event` / past-tense verbs; Java classes annotated `@DomainEvents` or extending `ApplicationEvent`; Python dataclasses with `frozen=True` and past-tense names.

### Services

- Class name ends in `Service` and the class is registered in DI as a non-singleton (scoped / transient) that takes one or more repositories or other domain services as constructor arguments.
- The class holds no mutable instance state — fields are all `readonly` and set in the constructor; behaviour is expressed as methods that coordinate two or more aggregates.
- The class lives in `Domain/` (a domain service) rather than `Application/` (an application service / use-case handler). A `Service` in `Application/` is more often a use-case orchestrator and should not be confused with a domain service.
- Anti-signal: the class has no methods other than CRUD pass-throughs to a single repository — this is "DI-only service with no behaviour" and is flagged against `## Known failure modes`.
- Other stacks (best-effort): Java `@Service` annotated classes; TS classes in `services/` injected via NestJS / InversifyJS containers; Python service modules grouped under `domain/services/`.

### Value objects

- Class or `record` / `record struct` with all properties `get`-only or `init`; equality is structural (overrides `Equals` / `GetHashCode`, or uses `record`'s synthesized equality).
- No identity field — value objects compare by composition, not by ID.
- Constructor validates the composed value (e.g., `Money(amount, currency)` rejects negative amounts or unknown currency codes) and the type is used wherever a primitive obsession would otherwise appear.
- Typical names: `Money`, `Address`, `EmailAddress`, `DateRange`, `Quantity`, `Sku` — single-concept noun phrases without an `Id` suffix.
- Other stacks (best-effort): Java classes annotated `@Embeddable` or libraries like Lombok `@Value`; TS classes / branded types under `value-objects/`; Python `frozen=True` dataclasses with `__post_init__` validation.

## Ubiquitous-language extraction heuristic

**Named heuristic: weighted cross-boundary symbol frequency.**

The intuition: terms that show up in many places across the codebase, _and_ cross project / namespace boundaries, are the strongest candidates for the repo-wide glossary. Terms that only appear inside one project are candidates for that bounded context's local glossary instead.

Step-by-step recipe:

1. **Collect raw symbols.** Walk every source file under `<path>` and emit the set of declared type names, public method names, and public property names. Skip files under `bin/`, `obj/`, `node_modules/`, `dist/`, `*.generated.cs`, `*.designer.cs`, test projects (`*Tests*`, `*.Tests`).
2. **Tokenize.** Split each symbol on PascalCase / camelCase boundaries (`OrderLineItem` -> `Order`, `Line`, `Item`). Lowercase the resulting tokens for counting; keep the original casing for display in the glossary.
3. **Drop stopwords.** Remove framework / generic tokens that carry no domain meaning: `Service`, `Repository`, `Controller`, `Handler`, `Dto`, `Request`, `Response`, `Get`, `Set`, `Async`, `Result`, `Options`, `Configuration`, `Factory`, `Builder`, `Manager`, `Helper`, plus standard library types (`String`, `Int`, `Task`, `List`, `Dictionary`).
4. **Score by frequency and reach.** For each remaining token, count (a) total occurrences and (b) the number of distinct top-level projects / namespaces it appears in. Score = `total_occurrences * distinct_projects`. The `distinct_projects` multiplier is what makes a term "cross-boundary".
5. **Cut at the elbow.** Sort by score descending; take the top N where N is chosen by visual inspection of the score distribution (typical elbow: top 30-60 terms for a small repo, top 100-200 for a medium one). The cut is heuristic, not a hard threshold.
6. **Bucket by reach.** Terms appearing in `>= 2` distinct projects / namespaces go into the repo-wide `glossary.md`. Terms appearing in only one project go into that BC's local `glossary.md`.
7. **Stub definitions from context.** For each candidate term, pull the XML doc-comment if present; otherwise pull the first sentence of the containing class's doc-comment; otherwise leave the definition as `TODO: define (seen in <project>/<file>)`. The agent must not fabricate definitions — empty / TODO is preferable to hallucinated meaning.

The output of this recipe is a candidate glossary term list, ready to be written into `glossary.md` files by the writer.

## Known failure modes

Four documented failure modes the agent must watch for. Each lists name, observable symptom, and recommended treatment.

- **Anemic domain model.**
  Symptom: aggregate / entity classes hold only public getter/setter properties and no behaviour methods; all logic lives in a sibling `*Service` class that mutates the entity directly. Treatment: still emit the aggregate doc, but include a top-of-file note flagging "behaviour-free aggregate — invariants likely live in <ServiceName>". Add an entry in the BC candidate report so the human reviewer can see it in the audit trail.

- **Generic repository everywhere.**
  Symptom: a single `IRepository<T>` / `IGenericRepository<T>` interface is used for every aggregate; no per-aggregate repository interfaces exist. Treatment: do not emit one `repositories.md` row per `T` discovered (that would be misleading); instead, emit a single `repositories.md` entry naming the generic interface and listing the `T` parameters observed, with a note that per-aggregate intent must be confirmed by the human. Cross-reference this failure mode in the candidate report.

- **EF Core entity mistaken for an aggregate root.**
  Symptom: a class is mapped via `DbSet<T>` on the `DbContext` and has public navigation collections (`public List<OrderLine> Lines { get; set; }`), but it has no encapsulating behaviour and no repository scoped to it. Many such classes are entities owned by another aggregate, not roots in their own right. Treatment: do not promote every `DbSet<T>` to an aggregate doc. Only emit `aggregates/<aggregate>.md` for classes that also pass at least one behavioural signal from `### Aggregates` above (private setters, constructor invariants, methods that mutate state, raised domain events). Demote the rest to entries inside the owning aggregate's invariants list, citing the EF mapping by `file:line`.

- **Dependency-injection-only services with no behaviour.**
  Symptom: a class registered in DI with `AddScoped<IFooService, FooService>` exposes methods that are 1:1 pass-throughs to a single repository (`return _repo.GetById(id);`). No coordination, no policy, no invariants. Treatment: omit from `services.md` as a domain service; mention in the BC candidate report under "Skipped services — likely thin wrappers". This avoids polluting the domain-services list with infrastructure plumbing.

## What to do when code-as-source-of-truth conflicts with itself

**Rule.** When two parts of the codebase disagree on the same invariant or the same calculation, the writer must (a) prefer the side with stricter invariants enforced in the constructor / domain method, and (b) surface the conflict in the BC candidate report so the human reviewer can see it in the audit trail; then files are written.

**Hypothetical .NET example.** Suppose `Order.Total` is computed two ways in the same repo: once as a method on the `Order` aggregate that sums `_lines.Sum(l => l.UnitPrice * l.Quantity)` and applies a discount via a `Discount` value object, and again as a column projection on `OrderListDto` that recomputes the total as `Lines.Sum(l => l.UnitPrice * l.Quantity)` and forgets the discount. The aggregate side is stricter (it includes the discount invariant); the projection side is laxer (it silently drops the discount). The writer prefers the aggregate's definition for the `Order.Total` invariant in `aggregates/Order.md`, and adds a candidate-report entry "Conflict: `OrderListDto.Total` projection drops the `Discount` rule enforced in `Order.RecalculateTotal()`".

**Surface, do not silently pick.** The writer MUST NOT pick one side without recording the conflict. If the writer detects two divergent definitions and emits only one of them with no note in the candidate report, the human reviewer has no way to spot the bug — and code-as-source-of-truth has just become code-as-source-of-bug-disguised-as-truth. The minimum acceptable behaviour is: pick the stricter side for the canonical doc, AND emit a "Conflicts detected" subsection in the BC candidate report listing every divergence with both `file:line` anchors for the audit trail.

## Soft input from `docs/narrative/`

When `docs/narrative/architecture.md` and/or `docs/narrative/<bc>/walkthrough.md` files exist in the working directory at runtime, they are consumed as **soft input** to BC candidate surfacing per `SKILL.md` `## Soft input: docs/narrative/`. The narrative is *input*, not *source of truth*: code remains authoritative for every `file:line` citation and every output-schema row. See `SKILL.md` `## Soft input: docs/narrative/` `### Hallucination guard (narrative variant)` for the no-hallucination invariant. When `docs/narrative/` is absent, behaviour is byte-identical to runs before this section was added — the soft-input read is a strict no-op.
