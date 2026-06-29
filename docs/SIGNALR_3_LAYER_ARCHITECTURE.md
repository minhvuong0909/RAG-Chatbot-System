# SignalR 3-Layer Architecture

```mermaid
flowchart TB
    user["User / Browser"]

    subgraph presentation["Presentation Layer"]
        direction LR
        razor["Razor Pages<br/>(.cshtml + PageModel)"]
        api["Controllers / API Endpoints"]
        client["SignalR Client JS"]
        hub["SignalR Hubs<br/>(ChatHub, DocumentHub, NotificationHub)"]
    end

    subgraph business["Business Layer"]
        direction LR
        services["Services"]
        dtos["DTOs"]
        realtime["Realtime Event Dispatcher"]
        ragflow["RAG / Chat Orchestration"]
    end

    subgraph data["Data Access Layer"]
        direction LR
        repo["Repository / UnitOfWork"]
        dbcontext["AppDbContext"]
        models["Models / Entities"]
    end

    subgraph database["Database"]
        postgres["PostgreSQL + pgvector"]
    end

    subgraph external["External Services"]
        direction TB
        ragapi["Python RAG API<br/>FAISS + BM25 + Reranker"]
        llm["LLM Provider"]
        storage["File Storage<br/>wwwroot/uploads<br/>Google Drive optional"]
    end

    user --> razor
    user <--> client
    client <--> hub

    razor --> services
    api --> services
    hub --> services

    services --> ragflow
    services --> repo
    repo --> dbcontext
    dbcontext --> models
    dbcontext --> postgres

    ragflow --> ragapi
    ragflow --> llm
    services --> storage

    services -. realtime events .-> realtime
    realtime -. chat chunks .-> hub
    realtime -. document progress .-> hub
    realtime -. notifications .-> hub
    hub -. push updates .-> client
    client -. update UI .-> user

    classDef outer fill:#fff8dc,stroke:#c8b46a,stroke-width:1px,color:#111;
    classDef node fill:#eadff0,stroke:#8b7a96,stroke-width:1px,color:#111;
    classDef user fill:#dbeafe,stroke:#5b7da8,stroke-width:1px,color:#111;
    classDef realtime fill:#dcfce7,stroke:#15803d,stroke-width:1px,color:#111;

    class presentation,business,data,database,external outer;
    class razor,api,services,dtos,repo,dbcontext,models,postgres,ragapi,llm,storage,ragflow node;
    class user user;
    class client,hub,realtime realtime;
```

## What Changed From The Old Diagram

- The architecture still keeps the same 3 layers: Presentation, Business, and Data Access.
- The Presentation Layer changes from MVC-only UI to Razor Pages plus SignalR.
- SignalR Hubs stay in the Presentation Layer because they are ASP.NET endpoints.
- Business services remain the place for chat, document, admin, and permission logic.
- Realtime events are emitted from the Business Layer and pushed to the browser through SignalR Hubs.

## Realtime Features Added

- Realtime chat response updates.
- AI answer chunk streaming.
- Document processing progress.
- Admin approval and permission notifications.
- Auto-update chat session and document sidebars.
