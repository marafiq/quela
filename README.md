# Quela Reactive Orchestration Runtime

A full-stack reactive orchestration runtime for .NET that controls UI form fields, validations, network requests, and complex workflows through a fluent DSL compiled to a static reactive execution graph.

## Features

- **Reactive DAG Runtime**: Deterministic execution with automatic dependency tracking
- **Transactional Orchestration**: ACID-like guarantees for state changes
- **Fluent DSL**: Intuitive API for defining orchestrations
- **Declarative UI Patches**: Minimal JSON patches instead of full HTML reloads
- **Effect Isolation**: Retries, timeouts, cancellation, and idempotency
- **ASP.NET Core Integration**: Drop-in minimal API support

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     QUELA RUNTIME PIPELINE                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  RECEIVE      VALIDATE      SCHEDULE      EXECUTE               │
│  Changeset ──▶ & Parse ──▶  Nodes   ──▶  Effects               │
│                                              │                   │
│  EMIT         GENERATE      COMMIT          │                   │
│  Patches ◀── Patches   ◀── Transaction ◀───┘                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Quick Start

### 1. Install Packages

```bash
dotnet add package Quela.Reactive.AspNetCore
```

### 2. Define an Orchestration

```csharp
using Quela.Reactive.DSL;

var graph = OrchestrationBuilder.Create("user-form", "User Registration")
    // Define a field with validation
    .Field<string>("email")
        .Default("")
        .Required("Email is required")
        .Email("Invalid email format")
        .BindTo("#email")
        .Debounce(300)
        .Add()

    // Define a computed field
    .Computed<bool>("isValid")
        .DependsOn("email", "password")
        .Compute(ctx =>
            !string.IsNullOrEmpty(ctx.Get<string>(new("email"))) &&
            !string.IsNullOrEmpty(ctx.Get<string>(new("password"))))
        .Add()

    // Define an effect (API call)
    .Effect<UserResult>("createUser")
        .DependsOn("submit", "email", "password")
        .WithRetry(3)
        .WithTimeout(30)
        .ExecuteAsync(async ctx =>
        {
            var email = ctx.Get<string>(new("email"));
            // Call your API
            return new UserResult { UserId = "123" };
        })
        .Add()

    // Define a trigger (button)
    .Trigger("submit")
        .OnSubmit()
        .RequiresValid("email", "password")
        .BindTo("#submit-btn")
        .Add()

    .Build();
```

### 3. Register with ASP.NET Core

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Quela services
builder.Services.AddQuelaReactive();
builder.Services.AddOrchestration(graph);

var app = builder.Build();

// Map Quela endpoints
app.MapQuelaEndpoints("/api/quela");

app.Run();
```

### 4. Client Integration

```javascript
// Create a session
const session = await fetch('/api/quela/sessions', {
    method: 'POST',
    body: JSON.stringify({ orchestrationId: 'user-form' })
}).then(r => r.json());

// Send changes
const response = await fetch(`/api/quela/sessions/${session.sessionId}/changesets`, {
    method: 'POST',
    body: JSON.stringify({
        transactionId: crypto.randomUUID(),
        sessionId: session.sessionId,
        orchestrationId: 'user-form',
        timestamp: Date.now(),
        changes: [
            { nodeId: 'email', value: 'user@example.com', source: 'Input' }
        ],
        actions: []
    })
}).then(r => r.json());

// Apply patches to the DOM
response.patches.forEach(patch => {
    const el = document.querySelector(patch.target);
    switch (patch.operation) {
        case 'SetValue':
            el.value = patch.value;
            break;
        case 'SetVisible':
            el.style.display = patch.value ? '' : 'none';
            break;
        // ... handle other operations
    }
});
```

## Core Concepts

### Node Types

| Type | Description |
|------|-------------|
| `Value` | Mutable input (form fields) |
| `Computed` | Derived value from dependencies |
| `Effect` | Side effect (API calls, I/O) |
| `Conditional` | Dynamic routing based on conditions |
| `Aggregator` | Fan-in from multiple sources |
| `Trigger` | Action initiator (buttons) |
| `Validation` | Async validation rules |
| `Partial` | Nested sub-graph |

### Effect Features

- **Retry Policies**: Configurable exponential backoff
- **Timeouts**: Per-effect timeout configuration
- **Idempotency**: Automatic deduplication via keys
- **Cancellation Scopes**: Grouped cancellation
- **Circuit Breaker**: Automatic failure isolation

### UI Patch Operations

| Operation | Description |
|-----------|-------------|
| `SetValue` | Set input/select value |
| `SetText` | Set text content |
| `SetVisible` | Toggle visibility |
| `SetEnabled` | Toggle enabled state |
| `SetValid` | Set validation state |
| `AddClass` | Add CSS class |
| `RemoveClass` | Remove CSS class |
| `InsertPartial` | Insert HTML content |

## Advanced Usage

### Parallel Effects (Fan-Out/Fan-In)

```csharp
// Multiple parallel API calls
.Effect<ShippingRates>("fetchShipping")
    .DependsOn("calculateTotals")
    .ExecuteAsync(async ctx => /* ... */)
    .Add()

.Effect<TaxResult>("calculateTax")
    .DependsOn("calculateTotals")
    .ExecuteAsync(async ctx => /* ... */)
    .Add()

// Aggregate results
.Aggregate<object, OrderSummary>("orderSummary")
    .FromSources("fetchShipping", "calculateTax")
    .WaitAll()
    .Aggregate(results => /* combine results */)
    .Add()
```

### Conditional Branches

```csharp
.When("showPaymentForm")
    .DependsOn("paymentMethod")
    .Condition(ctx => ctx.Get<string>(new("paymentMethod")) == "card")
    .ThenActivate("cardNumber", "cardExpiry", "cardCvv")
    .ElseActivate("bankAccount")
    .Add()
```

### Async Validation

```csharp
.Validation<string>("emailUniqueness")
    .For("email")
    .ValidateAsync(async (ctx, email, ct) =>
    {
        var exists = await CheckEmailExists(email, ct);
        return exists
            ? ValidationResult.Invalid("Email already registered")
            : ValidationResult.Valid();
    })
    .Debounce(TimeSpan.FromMilliseconds(500))
    .Add()
```

## Project Structure

```
src/
├── Quela.Reactive.Core/          # Core abstractions and models
│   ├── Graph/                    # Node and edge definitions
│   ├── Primitives/               # Value types (NodeId, TransactionId)
│   ├── Validation/               # Validation rules
│   ├── Effects/                  # Effect result types
│   ├── Patches/                  # UI patch model
│   └── Payloads/                 # Client/server payloads
├── Quela.Reactive.Runtime/       # Execution engine
│   ├── Execution/                # Orchestration engine
│   ├── Scheduling/               # Topological scheduler
│   ├── Transactions/             # Transaction manager
│   ├── Effects/                  # Effect runtime
│   └── State/                    # Session state
├── Quela.Reactive.DSL/           # Fluent DSL
│   ├── OrchestrationBuilder.cs   # Main builder API
│   ├── Extensions/               # Validation extensions
│   └── Compiler/                 # Graph compiler
├── Quela.Reactive.AspNetCore/    # ASP.NET Core integration
│   ├── Endpoints/                # Minimal API endpoints
│   ├── Sessions/                 # Session management
│   └── Registry/                 # Orchestration registry
└── Quela.Reactive.Client/        # .NET client
```

## Design Principles

1. **Deterministic Execution**: All state transitions are reproducible
2. **Graph Scheduling**: Optimal parallel execution via wave-based scheduling
3. **Effect Isolation**: Side effects are tracked and can be retried/cancelled
4. **Execution Safety**: Transactional guarantees for state consistency
5. **Static Analysis**: Graphs are validated at compile time

## License

MIT License - see LICENSE file for details.
