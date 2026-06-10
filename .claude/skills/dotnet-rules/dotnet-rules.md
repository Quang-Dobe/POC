# .NET Engineering Rules - Vibe Coding Agent Ruleset

> **Architectural style**: **Clean Architecture + CQRS-Lightweight**. Layers (`Domain <- Application <- Infrastructure <- Entry-point`) plus an in-house, MediatR-style dispatcher (`ISender` + `IRequest<TResponse>` + `IRequestHandler<,>` + `IPipelineBehavior<,>`) that lives in the Application layer. **MediatR and any other third-party CQRS framework are forbidden** - see Section 3.0 and Section 11.

> **Directive**: These rules are **mandatory**. The agent MUST follow every rule strictly. Any generated code that violates these rules is considered invalid and must be regenerated. No exceptions unless explicitly overridden by the user in writing.

---

## 0. Meta Rules

- **NEVER generate code that compiles but violates architecture rules.** Compilation success != correctness.
- **NEVER skip layers** to "save time". Every request passes through the correct architectural boundary.
- **NEVER use magic strings, magic numbers, or inline literals** without named constants or configuration.
- **ALWAYS prefer explicitness over brevity** when brevity sacrifices clarity.
- **ALWAYS ask for clarification** before generating code for ambiguous domain concepts.
- Generated code must be **production-grade by default** - not demo-grade, not prototype-grade.

---

## 1. Solution & Project Structure

### 1.1 Layered Solution Layout

```
Solution.sln
|
+-- src/
|   +-- Domain/                   # Core business logic - zero external dependencies
|   +-- Application/              # Use cases, commands, queries, interfaces
|   +-- Infrastructure/           # EF Core, external APIs, messaging, file I/O
|   +-- API (or Worker)/          # Entry points: Web API controllers, background services
|
+-- tests/
|   +-- Domain.Tests/
|   +-- Application.Tests/
|   +-- Integration.Tests/
|
+-- docs/
    +-- architecture-decisions/   # ADR files
```

### 1.2 Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Solution | `CompanyName.ProductName` | `Acme.Billing` |
| Project | `[Solution].[Layer]` | `Acme.Billing.Domain` |
| Namespace | Matches folder path exactly | `Acme.Billing.Domain.Invoices` |
| Class | PascalCase, noun or noun-phrase | `InvoiceService`, `PaymentGateway` |
| Interface | `I` + PascalCase noun | `IInvoiceRepository`, `IPaymentService` |
| Method | PascalCase verb or verb-phrase | `CalculateTax`, `SendConfirmationEmail` |
| Variable/param | camelCase | `invoiceId`, `customerEmail` |
| Private field | `_camelCase` | `_invoiceRepository` |
| Constant | PascalCase | `MaxRetryCount`, `DefaultPageSize` |
| Enum | PascalCase, singular | `InvoiceStatus`, `PaymentMethod` |
| Async method | Suffix `Async` | `GetByIdAsync`, `ProcessPaymentAsync` |

### 1.3 File Layout Rules

- **One class (or record or interface) per file.**
- File name **must exactly match** the type name.
- Group files by **feature/aggregate**, not by type (no "Controllers/", "Services/", "Repositories/" mega-folders).

```
# CORRECT
Domain/
  Invoices/
    Invoice.cs
    InvoiceItem.cs
    InvoiceStatus.cs
    IInvoiceRepository.cs
    InvoiceDomainService.cs

# WRONG
Domain/
  Entities/
    Invoice.cs
    InvoiceItem.cs
  Enums/
    InvoiceStatus.cs
```

---

## 2. Domain Layer Rules

### 2.1 Entities

- Entities are **identified by an ID**, not by their property values.
- Entities **encapsulate their own invariants** - never expose raw setters for business-critical state.
- Use **private or protected setters**; mutate state only via explicit domain methods.
- Entity IDs **must be strongly typed** - never use bare `int`, `Guid`, or `string` as IDs across domain boundaries.

### 2.2 Strongly Typed IDs

```csharp
public readonly record struct InvoiceId(Guid Value)
{
    public static InvoiceId NewId() => new(Guid.NewGuid());
    public static InvoiceId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

### 2.3 Value Objects

- Value objects have **no identity** - equality is by structural value.
- Value objects are **immutable**.
- Place validation inside the constructor or factory method.
- **Never** share mutable state between value objects.

### 2.4 Aggregates

- Each aggregate has a single **Aggregate Root** - the only entry point for external modifications.
- Aggregate boundaries enforce **consistency** - everything inside an aggregate is consistent at commit time.
- **Do NOT reference** another aggregate's internal entities - reference only by ID.
- Keep aggregates **small**. If an aggregate has more than 5-7 direct children, reconsider the boundary.

### 2.5 Domain Events

- Domain events express **facts that have happened** - past tense, immutable.
- Raise domain events **inside the aggregate**, not outside.
- Events are **collected** on the aggregate root and dispatched **after persistence** via the dispatcher (Section 3.0) or a domain-event dispatcher.

### 2.6 Domain Services

- Use a Domain Service only when a business operation **does not naturally belong** to a single entity or value object.
- Domain Services are **stateless**.
- Do NOT inject infrastructure concerns (e.g., `DbContext`, `HttpClient`) into Domain Services.

### 2.7 Repository Interfaces

- Define repository **interfaces in the Domain layer**.
- Implementations live in **Infrastructure**.
- Repositories operate on **aggregates only** - never on individual child entities.

---

## 3. Application Layer Rules

### 3.0 CQRS Dispatcher - Lightweight, In-House (No MediatR)

The Application layer ships its **own** CQRS dispatcher under `Common/Dispatching/`. **MediatR (and any equivalent third-party CQRS framework) is forbidden.** The rationale: zero external dependency, full control of the pipeline, and the dispatcher is small enough to maintain in-tree.

The dispatcher exposes exactly four public contracts and one entry point:

```csharp
public interface IRequest<TResponse> { }

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
```

Rules:

- The Application layer **owns** the dispatcher. Never let `ISender` leak into Domain.
- Controllers, hosted services, and other entry points inject **`ISender`** - never an `IRequestHandler<,>` directly.
- Handlers are **one per request type**. No multi-handler / notification fan-out (that is a separate concept; if needed, add an explicit `IDomainEventDispatcher`, do not overload `ISender`).
- The dispatcher resolves the handler and the **ordered list** of `IPipelineBehavior<,>` from the DI container at send time. Pipeline order is determined by **DI registration order** (see Section 10.3).
- The dispatcher is **stateless** and registered as `Transient`.
- **Never** call `Activator.CreateInstance` on a handler from application code - only the dispatcher composes handlers.

### 3.1 CQRS - Commands & Queries

- **Strict CQRS separation**: commands mutate state, queries read state. Never mix.
- Dispatch both through **`ISender`** - the in-house lightweight dispatcher (Section 3.0). No MediatR.
- A command returns **nothing meaningful** (`Result`) or a **result/identifier** only (`Result<TId>`) - never full domain objects.
- A query returns a **DTO** wrapped in `Result<TDto>` - never a domain entity.
- Both commands and queries implement `IRequest<TResponse>` from `Common/Dispatching/`.

### 3.2 Command Handlers

Handlers depend on abstractions only (repositories, services), keep methods small, and return `Result` / `Result<T>` exclusively.

### 3.3 Validation - FluentValidation

- **Every command MUST have a validator** - no exceptions.
- Validators live in the same file or a sibling file to the command, in the Application layer.
- The dispatcher's **`ValidationPipelineBehavior<TRequest, TResponse>`** runs all registered `IValidator<TRequest>` instances automatically. Handlers MUST NOT re-validate.
- On validation failure the behavior **short-circuits** and returns `Result.Failure(error)` / `Result<T>.Failure(error)` via `ResultFactory`. It NEVER throws `ValidationException` - that breaks the Result contract (Section 3.5).
- If a request's `TResponse` is neither `Result` nor `Result<T>`, the behavior throws `InvalidOperationException` at runtime. **Never** declare a command/query that returns anything else.

### 3.4 DTOs

- DTOs are **records** - immutable by default.
- DTOs **never expose** domain types (value objects, enums from domain) directly - map to primitives or application-layer enums.
- Use explicit mapping methods (or AutoMapper profiles as a last resort).

### 3.5 Result Pattern

- **Never throw exceptions for expected failures** (not found, validation error, business rule violation, parse error, I/O failure that the caller can act on).
- Use a `Result` / `Result<T>` pair consistently across the Application layer. **Every** command and query returns `Result` or `Result<T>` - no naked DTOs, no `T?`, no `bool` success flags.
- A `Result` has two invariants enforced in its constructor:
  1. A successful result MUST carry `Error.None`.
  2. A failed result MUST carry a non-`None` `Error`.
- Errors are records: `Error(string Code, string Message, ErrorType Type)`. Group per-aggregate error catalogs in a `static class {Aggregate}Errors` next to the aggregate's commands/queries.
- `ErrorType` is a closed enum: `None`, `NotFound`, `Validation`, `Conflict`, `Unexpected`. Add new values only when you also extend the API-layer `ErrorType -> HTTP status` mapping (Section 5.2). Do **not** invent ad-hoc error categories at the call site.
- `OperationCanceledException` is the **only** exception that crosses the Result boundary - pipeline behaviors re-throw it as-is (Section 7.5, Section 10.3). Cancellation is not a failure to be wrapped in `Result.Failure`.

### 3.6 Application Service Interfaces

- Define interfaces for **external services** (email, payment, SMS) in the Application layer.
- Implementations live in Infrastructure.

---

## 4. Infrastructure Layer Rules

### 4.1 EF Core - DbContext

- Use **one DbContext per bounded context**.
- Apply all configurations via `IEntityTypeConfiguration<T>` - **never** use data annotations on domain entities.
- Configure value object conversions and strongly typed ID conversions explicitly.

### 4.2 Repository Implementations

Implement Domain interfaces; return `IReadOnlyList<T>` (never `IEnumerable<T>`); honor `CancellationToken`.

### 4.3 Unit of Work

`IUnitOfWork.SaveChangesAsync` is the single commit boundary. Domain events collected during the change are dispatched after the underlying `SaveChangesAsync` succeeds.

### 4.4 Migrations

- **Never use `EnsureCreated()`** in production code paths.
- Migrations run at **startup via `IHostedService`** or deployment pipeline - not inline in request handlers.
- Migration files are **never hand-edited** after being applied to any environment.

---

## 5. API Layer Rules

### 5.1 Controller Design

- Controllers are **thin** - they translate HTTP -> Application command/query -> HTTP response.
- **No business logic** in controllers.
- **No domain types** returned from controllers - always DTOs or problem details.
- Use `[ApiController]`, `[Route]`, and explicit `[Http*]` attributes on every action.

### 5.2 Error Responses

- Use **RFC 9457 Problem Details** (`ProblemDetails`) for all error responses.
- Map domain errors -> HTTP status codes consistently via extension methods:
  - `NotFound` -> 404
  - `Validation` -> 422
  - `Conflict` -> 409
  - `Unexpected` -> 500
- **Never** return raw exception messages to the client.

### 5.3 Versioning

- **All APIs must be versioned** from day one. Use `Asp.Versioning`.
- Default version: `v1`. Never remove versions - deprecate instead.

### 5.4 Security

- **Authentication and authorization are configured at the infrastructure level**, not in individual controllers where avoidable.
- Use `[Authorize]` policies - never `[Authorize(Roles = "Admin")]` with hard-coded role strings.
- Sensitive config (connection strings, API keys) **must use** `IOptions<T>` bound from environment variables or secrets manager - never `IConfiguration["Key"]` inline.

---

## 6. SOLID Principles - Enforcement Rules

### S - Single Responsibility Principle

- **One reason to change per class.** If a class has methods that serve different actors (e.g., persisting AND sending emails), split it.
- Validators, mappers, event handlers = separate classes.
- Violation signal: class name contains "And", "Or", "Manager", "Helper", "Util" (red flags - rename and refocus).

### O - Open/Closed Principle

- Extend behavior via **new classes**, not by modifying existing ones.
- Use **Strategy pattern**, **pipeline behaviors**, or **decorators** for cross-cutting concerns (logging, caching, retry).
- Configuration/feature flags **over** conditional logic branching in core classes.

### L - Liskov Substitution Principle

- Any implementation of an interface must be **fully substitutable** without behavioral changes.
- **Never** throw `NotImplementedException` in interface implementations.
- **Never** ignore method parameters silently in overrides.

### I - Interface Segregation Principle

- Interfaces should have **as few methods as possible**.
- Prefer multiple focused interfaces over one fat interface.
- If a consumer only uses 2 of 8 interface methods, the interface must be split.

### D - Dependency Inversion Principle

- High-level modules (Application, Domain) **never depend** on low-level modules (Infrastructure, API).
- **Always depend on abstractions** (interfaces), not concrete implementations.
- Use constructor injection - **never** service locator, `new` for dependencies, or static accessors.

---

## 7. Clean Code Rules

### 7.1 Naming

- Names must **reveal intent**. If you need a comment to explain a variable name, rename it.
- Avoid abbreviations (`cust` -> `customer`, `inv` -> `invoice`).
- Boolean names must read as assertions (`isActive`, `hasPermission`, `canSubmit`).

### 7.2 Functions & Methods

- Methods do **one thing**. If a method description requires "and", split it.
- Maximum method length: **20-30 lines** of meaningful code. Longer = extract.
- Maximum parameters: **3**. Use a parameter object for more.
- No side effects in query methods - they must be **referentially transparent**.

### 7.3 Comments

- **Do not comment what** - the code says what. Comment **why** when non-obvious.
- Commented-out code is **forbidden** - use version control.
- TODO comments require a linked issue ID: `// TODO(#123): Implement retry logic`

### 7.4 Error Handling

- **Never swallow exceptions silently.** At minimum, log before re-throwing.
- **Never** use `catch (Exception ex)` at a business logic level - catch specific exceptions.
- **Never** use exceptions for flow control - use the Result pattern.
- **Always** propagate `CancellationToken` through async call chains.

### 7.5 Async/Await

- **Every async method returns `Task` or `Task<T>`** - never `async void` (except event handlers).
- Always `await` - never `.Result` or `.Wait()` (deadlock risk).
- Always pass and honor `CancellationToken` in all I/O operations.
- Use `ConfigureAwait(false)` in library/infrastructure code.
- `OperationCanceledException` propagates - never wrap it in a `Result.Failure` or swallow it.

### 7.6 Null Handling

- Enable **nullable reference types** (`<Nullable>enable</Nullable>`) in all projects.
- **Never** return `null` from a public method - return `Option<T>`, `Result<T>`, or an empty collection.
- Use `ArgumentNullException.ThrowIfNull()` at public method entry points.
- Prefer `??`, `?.`, and pattern matching over null checks.

---

## 8. Dependency Injection & Registration Rules

### 8.1 Lifetime Rules

| Service Type | Lifetime |
|---|---|
| DbContext | Scoped |
| Repository | Scoped |
| Unit of Work | Scoped |
| Domain/Application Services | Scoped or Transient |
| HttpClient (via IHttpClientFactory) | Transient (managed by factory) |
| Caches, configs | Singleton |
| `ISender` / `IRequestHandler<,>` / `IPipelineBehavior<,>` / `IValidator<>` | Transient |

### 8.2 Registration Pattern

- Each layer registers its own services via an **extension method** on `IServiceCollection`.
- `Program.cs` / `Startup.cs` calls layer-level extension methods only - no raw `services.AddScoped<>()` chains in the entry point.
- The Application layer exposes **one** extension method (e.g., `AddApplicationDispatcher`) - composition root calls it as a single line.
- Handlers and validators are registered by **reflection scan** of the Application assembly. Do not hand-register handlers one by one - that drifts and rots.
- Pipeline behaviors are registered **explicitly and in order**. The dispatcher honors registration order; reordering these four lines reorders the runtime pipeline. Do not rely on alphabetical or file order.
- **Never** register a handler or behavior in the entry-point project (`Program.cs`). The Application layer owns its own composition.

---

## 9. Testing Rules

### 9.1 Unit Tests

- **Every domain method with business logic has a unit test**.
- Tests follow **Arrange / Act / Assert** structure with clear sections.
- Test method names: `MethodName_StateUnderTest_ExpectedBehavior`.
- Use **xUnit** + **FluentAssertions** + **NSubstitute** (or Moq).
- **No I/O in unit tests** - all external dependencies are mocked.

### 9.2 Integration Tests

- Integration tests use **real infrastructure** (Testcontainers for Docker-based DBs).
- Use `WebApplicationFactory<T>` for API-level integration tests.
- Every integration test cleans up its own data (use transactions or respawn).

### 9.3 Test Coverage Expectations

| Layer | Minimum Coverage Target |
|---|---|
| Domain (entities, VOs, domain services) | 90%+ |
| Application (command/query handlers) | 80%+ |
| Infrastructure | Integration tests only |
| API (controllers) | Integration tests only |

---

## 10. Cross-Cutting Concerns

### 10.1 Logging

- Use **`ILogger<T>`** - never `Console.Write`, never static loggers.
- Use **structured logging** with named placeholders: `_logger.LogInformation("Invoice {InvoiceId} submitted", id)`.
- Log levels: `Debug` = diagnostic; `Information` = business events; `Warning` = expected issues; `Error` = unexpected failures; `Critical` = system failures.
- **Never log sensitive data** (passwords, PII, payment details).

### 10.2 Configuration

- All configuration is **strongly typed** via `IOptions<T>`.
- Validate options at startup with `ValidateDataAnnotations()` and `ValidateOnStart()`.

### 10.3 Pipeline Behaviors (Lightweight Dispatcher)

The in-house dispatcher (Section 3.0) composes behaviors in **DI registration order** - first registered = outermost. Implement and register the following four behaviors, in this exact order:

1. **`LoggingPipelineBehavior`** (outermost) - log request type name on entry; on exit log success / warn-on-failure when the response is `Result`/`Result<T>`, otherwise log completion. Never logs request payloads (PII risk).
2. **`ValidationPipelineBehavior`** - run all `IValidator<TRequest>` instances in parallel; on failure short-circuit with `Result.Failure(error)` (never throw `ValidationException`). See Section 3.3.
3. **`PerformancePipelineBehavior`** - `Stopwatch` around `next()`; emit `Warning` when elapsed exceeds the configured slow threshold (default 500 ms). Never throws.
4. **`ExceptionHandlingPipelineBehavior`** (innermost) - catch any `Exception` thrown by the handler, log `Error`, convert to `Result.Failure(new Error("Unexpected", ex.Message, ErrorType.Unexpected))` via `ResultFactory`. **Re-throw `OperationCanceledException` unchanged** - cancellation is not a failure.

Why this order:

- Logging outermost = sees every request including those that fail validation or throw.
- Validation before Performance = a request rejected at validation does not skew latency stats with a sub-millisecond pass.
- ExceptionHandling innermost = catches handler exceptions but does **not** swallow exceptions thrown by behaviors above it (which should fail loudly - they indicate dispatcher misconfiguration, not business failures).

Adding a new cross-cutting concern (caching, transaction wrapping, audit, idempotency): write a new `IPipelineBehavior<,>` and register it in the correct slot. **Never** modify the existing four; **never** add cross-cutting logic inside handlers.

### 10.4 Health Checks

- **Always** register health checks for critical dependencies: DB, external APIs, message brokers.
- Expose at `/health/live` and `/health/ready`.

---

## 11. Forbidden Patterns

The agent **must never generate** the following:

| Pattern | Reason |
|---|---|
| `public List<T>` on entities | Breaks encapsulation; use `IReadOnlyCollection<T>` |
| `static` service classes | Untestable, hidden coupling |
| Business logic in controllers | Violates layered architecture |
| `DbContext` injected into Application layer | Violates dependency direction |
| `.Result` or `.Wait()` on tasks | Deadlock risk |
| Catching and swallowing exceptions silently | Hides failures |
| Magic strings for configuration keys | Breaks maintainability |
| Returning domain entities from Application layer | Leaks domain model to consumers |
| `new` for injected dependencies | Violates DIP |
| Bare `Guid`/`int` as entity IDs crossing layers | Type-unsafe, intent-obscuring |
| `async void` methods (except event handlers) | Unobservable exceptions |
| Nullable reference types disabled | Hides null bugs |
| Data annotations on domain entities | Couples domain to persistence concerns |
| God classes / God services | Violates SRP |
| `IEnumerable` returned from repositories | Multiple enumeration risk; use `IReadOnlyList<T>` |
| Adding **MediatR** (or another CQRS framework) | The lightweight in-house dispatcher (Section 3.0) is the only allowed dispatcher |
| Injecting `IRequestHandler<,>` directly into controllers / hosted services | Bypasses the pipeline. Inject `ISender` instead |
| Throwing `ValidationException` (or any exception) for an expected validation failure | Breaks the Result contract (Section 3.5) |
| Returning anything other than `Result` / `Result<T>` from a command/query handler | The validation and exception-handling behaviors require this shape |
| Catching `OperationCanceledException` and returning a `Result.Failure` | Cancellation is a control-flow signal, not a domain failure |
| Cross-cutting logic (logging, retry, caching, transactions) inside a handler | Add a new `IPipelineBehavior<,>` instead |
| Reordering pipeline behavior registrations without updating Section 10.3 | Order is observable behavior; treat it as part of the public contract |

---

## 12. Agent Checklist Before Outputting Code

Before generating any code, the agent MUST verify:

- [ ] Does this code belong to the correct layer?
- [ ] Are all external dependencies injected via constructor (never newed)?
- [ ] Are entity setters private or protected?
- [ ] Are all async methods using `CancellationToken`?
- [ ] Is the Result pattern used instead of throwing for expected failures?
- [ ] Are nullable reference types enabled and honored?
- [ ] Is there a validator for every command?
- [ ] Are strongly typed IDs used for all entity references?
- [ ] Are domain events raised inside the aggregate?
- [ ] Are collections on entities exposed as `IReadOnlyCollection<T>`?
- [ ] Does the controller return DTOs - never domain entities?
- [ ] Is `IOptions<T>` used for all configuration access?
- [ ] Is there a unit test structure implied or provided for the logic?
- [ ] Does every command/query implement `IRequest<Result>` or `IRequest<Result<T>>`?
- [ ] Is the entry point (controller / hosted service) injecting **`ISender`** - never `IRequestHandler<,>` directly?
- [ ] Are cross-cutting concerns implemented as `IPipelineBehavior<,>` rather than inside handlers?
- [ ] Is **MediatR (or any third-party CQRS framework) absent** from the project file?

---

*Architectural style: Clean Architecture + CQRS-Lightweight (in-house dispatcher, no MediatR). Rules apply to all new code and all refactors.*
