## ATAMO System Architecture
## ATAMO System Architecture

### Core Principles
- Highly configurable, auditable, smart messaging system
- Embeddable in .NET Core apps or runnable as a standalone REST service
- Async-first design with pluggable Agents and Config Providers

### High-Level Component Diagram

```mermaid
flowchart LR
    subgraph External
        EP[Event Providers / Clients]
        UI[Client UI / REST Clients]
    end

    subgraph HubCore[Hub (core engine)]
        direction TB
        HR[IHubReceiver]
        ES[EventStack / Queues]
        RT[Router / Rule Evaluator]
        AM[AgentManager]
        TS[Telemetry / Audit]
        PS[Persistence / Message Store]
    end

    subgraph Control[Controller / Host]
        HC[IHubControl]
        GOV[Governor / Monitors]
    end

    subgraph Ext[Extensions]
        CP[Config Providers]
        AG[Agents]
    end

    EP --> HR
    UI --> HR
    HR --> ES
    ES --> RT
    RT --> CP
    CP --> RT
    RT --> AM
    AM --> AG
    AG -->|responses / progress| AM
    AM --> TS
    TS --> PS

    HC --> HubCore
    GOV --> TS
    GOV --> PS

    style HubCore fill:#fff2b8,stroke:#333,stroke-width:1px
    style External fill:#e8f4ff,stroke:#333,stroke-width:1px
    style Control fill:#f2e8ff,stroke:#333,stroke-width:1px
    style Ext fill:#e8ffe8,stroke:#333,stroke-width:1px
```

Notes:
- EventProviders/Clients submit EventMessage or RequestMessage to `IHubReceiver` and receive an `EventKey`.
- `EventStack` queues incoming events; the `Router` evaluates event type and originating user against registered `Config Providers` to produce `ActionMessage`s.
- `AgentManager` dispatches `ActionMessage`s to `Agents`; Agents return progress/results which are recorded by `Telemetry / Audit` and persisted.
- `Controller` (`IHubControl`) hosts lifecycle operations (register agents/providers, configuration) and receives telemetry; `Governor` enforces monitoring, retention and alerting.
- Persistence and telemetry are pluggable to allow SQL, blob or cloud-backed stores.

See [docs/operational_flow.md](docs/operational_flow.md) and [docs/planned_docs.md](docs/planned_docs.md) for detailed flow and usage guides.
