# Quela Reactive Orchestration Runtime

## System Architecture Overview

The Quela Reactive Orchestration Runtime is a backend-driven reactive execution system that provides deterministic, transactional orchestration of UI interactions, network requests, and complex workflows through a fluent DSL compiled to a static reactive execution graph.

## Core Design Principles

1. **Deterministic Execution**: All state transitions are deterministic and reproducible
2. **Transactional Isolation**: Operations execute within transaction boundaries with ACID-like guarantees
3. **Reactive Dependency Graph**: Changes propagate through a DAG of dependent computations
4. **Effect Isolation**: Side effects are isolated, tracked, and can be retried/cancelled
5. **Declarative UI Patches**: UI updates are expressed as minimal JSON patches, not full reloads

---

## Runtime Model

### Execution Phases

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           QUELA RUNTIME PIPELINE                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌───────────┐ │
│  │   RECEIVE    │───▶│   VALIDATE   │───▶│   SCHEDULE   │───▶│  EXECUTE  │ │
│  │   Changeset  │    │   & Parse    │    │   Nodes      │    │  Effects  │ │
│  └──────────────┘    └──────────────┘    └──────────────┘    └───────────┘ │
│                                                                      │      │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐           │      │
│  │    EMIT      │◀───│   GENERATE   │◀───│    COMMIT    │◀──────────┘      │
│  │   Patches    │    │   Patches    │    │  Transaction │                  │
│  └──────────────┘    └──────────────┘    └──────────────┘                  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Transaction Pipeline

```
                     Transaction Lifecycle

    ┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
    │ PENDING │────▶│ RUNNING │────▶│COMMITTING────▶│COMMITTED│
    └─────────┘     └─────────┘     └─────────┘     └─────────┘
                          │
                          │         ┌─────────┐
                          └────────▶│ ABORTED │
                                    └─────────┘
```

---

## Node Types

The reactive graph consists of the following node types:

### 1. ValueNode
Represents a reactive value (form field, computed state, etc.)

```csharp
ValueNode<T> {
    Id: NodeId
    Value: T
    ValidationRules: ValidationRule[]
    Dependencies: NodeId[]
    Dependents: NodeId[]
}
```

### 2. ComputedNode
Derives value from dependencies through pure computation

```csharp
ComputedNode<T> {
    Id: NodeId
    ComputeFn: Func<IDependencyContext, T>
    Dependencies: NodeId[]
    CachedValue: T?
    IsDirty: bool
}
```

### 3. EffectNode
Represents a side effect (network request, I/O operation)

```csharp
EffectNode {
    Id: NodeId
    EffectFn: Func<IEffectContext, Task<EffectResult>>
    Dependencies: NodeId[]
    RetryPolicy: RetryPolicy
    TimeoutMs: int
    IdempotencyKey: string?
    CancellationScope: string
}
```

### 4. ConditionalNode
Routes execution based on conditions

```csharp
ConditionalNode {
    Id: NodeId
    Condition: Func<IDependencyContext, bool>
    TrueBranch: NodeId[]
    FalseBranch: NodeId[]
}
```

### 5. AggregatorNode
Fan-in aggregation from multiple sources

```csharp
AggregatorNode<T> {
    Id: NodeId
    Sources: NodeId[]
    AggregateFn: Func<T[], T>
    Strategy: AggregationStrategy // WaitAll, WaitAny, WaitN
}
```

### 6. PartialNode
Represents a nested UI partial with its own sub-graph

```csharp
PartialNode {
    Id: NodeId
    PartialId: string
    SubGraph: ReactiveGraph
    InputMappings: Dictionary<NodeId, NodeId>
    OutputMappings: Dictionary<NodeId, NodeId>
}
```

---

## Edge Model

Edges represent dependencies between nodes:

```csharp
Edge {
    Source: NodeId
    Target: NodeId
    Type: EdgeType // Data, Trigger, Conditional, Temporal
    Metadata: EdgeMetadata
}

EdgeMetadata {
    Debounce: TimeSpan?
    Throttle: TimeSpan?
    Transform: Func<object, object>?
    Filter: Func<object, bool>?
}
```

---

## Dependency Graph Structure

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                         REACTIVE DEPENDENCY GRAPH                            │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│    ┌─────────┐         ┌─────────┐         ┌─────────┐                      │
│    │ Field A │────────▶│Computed │────────▶│ Effect  │                      │
│    │ (Value) │         │  Node   │         │  Node   │                      │
│    └─────────┘         └─────────┘         └─────────┘                      │
│         │                   │                   │                            │
│         │                   │                   ▼                            │
│         │              ┌────┴────┐        ┌─────────┐        ┌─────────┐    │
│         │              │Condition│───────▶│Aggregator────────▶│ Partial │    │
│         │              │  Node   │        │  Node   │        │  Node   │    │
│         │              └─────────┘        └─────────┘        └─────────┘    │
│         │                   │                                     │          │
│         │                   ▼                                     ▼          │
│         │              ┌─────────┐                          ┌─────────┐     │
│         └─────────────▶│ Field B │                          │Sub-Graph│     │
│                        │ (Value) │                          │         │     │
│                        └─────────┘                          └─────────┘     │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## Execution Scheduler

### Topological Scheduling Algorithm

```
Algorithm: ScheduleExecution(dirtyNodes)

1. Identify all dirty nodes and their transitive dependents
2. Build execution wavefront (nodes with all dependencies satisfied)
3. For each wave:
   a. Execute all nodes in parallel within transaction
   b. Collect results
   c. Mark nodes as clean
   d. Add newly enabled nodes to next wavefront
4. On failure:
   a. Abort transaction
   b. Rollback all effects
   c. Return error patches
```

### Parallel Execution Strategy

```
┌────────────────────────────────────────────────────────────┐
│                    EXECUTION WAVES                         │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  Wave 1: ┌───┐ ┌───┐ ┌───┐                                │
│          │ A │ │ B │ │ C │  (independent, parallel)       │
│          └───┘ └───┘ └───┘                                │
│            │     │     │                                   │
│            ▼     ▼     ▼                                   │
│  Wave 2:     ┌───┐ ┌───┐                                  │
│              │ D │ │ E │    (depend on wave 1)            │
│              └───┘ └───┘                                  │
│                │     │                                     │
│                ▼     ▼                                     │
│  Wave 3:        ┌───┐                                     │
│                 │ F │       (depends on D and E)          │
│                 └───┘                                     │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## Effect Runtime

### Effect Execution Model

```csharp
EffectRuntime {
    // Execute with full lifecycle management
    ExecuteEffect(effect, context) {
        1. Check idempotency cache
        2. Acquire execution semaphore
        3. Start timeout timer
        4. Execute with cancellation token
        5. Handle retries on failure
        6. Record effect result
        7. Update idempotency cache
    }
}
```

### Retry Policy Configuration

```csharp
RetryPolicy {
    MaxRetries: int
    InitialDelay: TimeSpan
    MaxDelay: TimeSpan
    BackoffMultiplier: double
    RetryableExceptions: Type[]
    CircuitBreakerThreshold: int
}
```

---

## UI Patch Model

### Patch Types

```csharp
enum PatchOperation {
    SetValue,      // Set field value
    SetValid,      // Set validation state
    SetVisible,    // Toggle visibility
    SetEnabled,    // Toggle enabled state
    AddClass,      // Add CSS class
    RemoveClass,   // Remove CSS class
    SetAttribute,  // Set DOM attribute
    InsertPartial, // Insert partial HTML
    RemovePartial, // Remove partial
    ReplacePartial // Replace partial content
}

Patch {
    Target: string          // CSS selector or element ID
    Operation: PatchOperation
    Value: object
    TransactionId: Guid
    Sequence: long
}
```

### Patch Payload Format

```json
{
    "transactionId": "uuid",
    "sequence": 123,
    "patches": [
        {
            "target": "#email-field",
            "operation": "SetValue",
            "value": "user@example.com"
        },
        {
            "target": "#email-error",
            "operation": "SetVisible",
            "value": false
        },
        {
            "target": "#submit-btn",
            "operation": "SetEnabled",
            "value": true
        }
    ]
}
```

---

## Client ↔ Server Payload Model

### Changeset Payload (Client → Server)

```json
{
    "transactionId": "uuid",
    "sessionId": "uuid",
    "orchestrationId": "form-xyz",
    "timestamp": 1234567890,
    "changes": [
        {
            "nodeId": "email",
            "value": "user@example.com",
            "metadata": {
                "source": "input",
                "triggeredBy": "change"
            }
        }
    ],
    "actions": [
        {
            "actionId": "submit",
            "payload": {}
        }
    ]
}
```

### Response Payload (Server → Client)

```json
{
    "transactionId": "uuid",
    "status": "committed",
    "sequence": 124,
    "patches": [...],
    "effects": [
        {
            "effectId": "save-user",
            "status": "completed",
            "result": {...}
        }
    ],
    "nextActions": ["confirm", "redirect"],
    "errors": []
}
```

---

## Transaction Isolation Levels

```
┌─────────────────────────────────────────────────────────────┐
│                 TRANSACTION ISOLATION                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  SNAPSHOT ISOLATION:                                        │
│  - Each transaction sees consistent snapshot                │
│  - No dirty reads                                           │
│  - Optimistic concurrency control                           │
│                                                             │
│  SERIALIZABLE (optional):                                   │
│  - Full isolation between concurrent transactions           │
│  - Higher latency, guaranteed consistency                   │
│                                                             │
│  READ COMMITTED (default):                                  │
│  - See committed values from other transactions             │
│  - Good balance of consistency and performance              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Invalidation Algorithm

```
Algorithm: PropagateInvalidation(changedNode)

1. Mark changedNode as dirty
2. dirtySet = {changedNode}
3. queue = dependents(changedNode)

4. While queue not empty:
   a. node = queue.dequeue()
   b. If node not in dirtySet:
      - Mark node as dirty
      - Add node to dirtySet
      - Add dependents(node) to queue

5. Return dirtySet sorted topologically
```

---

## Cancellation Scopes

```csharp
CancellationScope {
    ScopeId: string
    ParentScope: CancellationScope?
    Token: CancellationToken
    ChildScopes: CancellationScope[]

    // Cancelling parent cancels all children
    Cancel() {
        foreach (child in ChildScopes)
            child.Cancel()
        TokenSource.Cancel()
    }
}
```

---

## Debouncing Strategy

```
┌─────────────────────────────────────────────────────────────────┐
│                      DEBOUNCE TIMELINE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Input:    A────B────C─────────────D────E────────────          │
│            │    │    │             │    │                       │
│  Debounce: │    │    │──[300ms]───▶│    │──[300ms]───▶         │
│  Window    │    │    │             │    │             │         │
│            │    │    │             │    │             │         │
│  Execute:  ─────────────────X──────────────────────X──          │
│                             C                      E            │
│                       (last in window)       (last in window)   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Fan-Out / Fan-In Pattern

```
                    FAN-OUT / FAN-IN AGGREGATION

              ┌─────────────────────────────────────┐
              │          TRIGGER NODE               │
              └─────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
        ┌──────────┐   ┌──────────┐   ┌──────────┐
        │ Effect 1 │   │ Effect 2 │   │ Effect 3 │
        │ (API A)  │   │ (API B)  │   │ (API C)  │
        └──────────┘   └──────────┘   └──────────┘
              │               │               │
              └───────────────┼───────────────┘
                              ▼
              ┌─────────────────────────────────────┐
              │      AGGREGATOR NODE                │
              │   Strategy: WaitAll / WaitAny       │
              └─────────────────────────────────────┘
                              │
                              ▼
              ┌─────────────────────────────────────┐
              │        RESULT NODE                  │
              └─────────────────────────────────────┘
```

---

## Memory Model

The runtime maintains the following state stores:

```csharp
RuntimeState {
    // Immutable graph definition
    Graph: ReactiveGraph

    // Mutable state (per-session)
    NodeValues: ConcurrentDictionary<NodeId, object>
    DirtyNodes: ConcurrentHashSet<NodeId>
    PendingEffects: ConcurrentQueue<EffectNode>

    // Transaction state
    ActiveTransactions: ConcurrentDictionary<Guid, Transaction>
    TransactionLog: AppendOnlyLog<TransactionRecord>

    // Idempotency
    IdempotencyCache: LRUCache<string, EffectResult>

    // Cancellation
    CancellationScopes: Dictionary<string, CancellationScope>
}
```

---

## Static Analysis Capabilities

The compiled graph supports:

1. **Cycle Detection**: Validates DAG property at compile time
2. **Type Checking**: Ensures dependency types match
3. **Reachability Analysis**: Identifies unreachable nodes
4. **Effect Dependency Analysis**: Tracks effect ordering requirements
5. **Deadlock Detection**: Identifies potential deadlocks in effect ordering

---

## Testing Model

```csharp
// Deterministic testing support
TestRuntime {
    // Mock effect results
    MockEffect(effectId, result)

    // Inject values
    SetValue(nodeId, value)

    // Execute single step
    StepExecution()

    // Assert state
    AssertNodeValue(nodeId, expected)
    AssertPatchEmitted(patch)

    // Time control
    AdvanceTime(duration)
}
```

---

## Performance Characteristics

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Invalidation propagation | O(V + E) | V = nodes, E = edges |
| Topological sort | O(V + E) | Pre-computed at compile time |
| Effect execution | O(n) parallel | n = wave size |
| Patch generation | O(d) | d = dirty nodes |
| Transaction commit | O(log n) | n = transaction log size |

---

## Security Considerations

1. **Effect Isolation**: Effects execute in sandboxed contexts
2. **Input Validation**: All inputs validated before graph entry
3. **Rate Limiting**: Built-in rate limiting per session
4. **Audit Logging**: All state transitions logged
5. **CSRF Protection**: Transaction IDs serve as CSRF tokens
