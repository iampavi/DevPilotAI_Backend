# DevPilot AI

**AI-powered Software Engineering Copilot for Enterprise Projects**

DevPilot AI is a production-grade Developer Intelligence Platform that understands entire codebases to help developers explore architecture, generate code, investigate bugs, write documentation, and execute agentic refactoring.

---

## 🏗️ Architecture Layout

The project follows Microsoft's Clean Architecture pattern directly in the root directory:

*   **`DevPilotAI.Api`**: Entry point. Contains Controllers, Configuration, Middleware (Global Error Handling), Serilog logger boots, and Swagger documentation setup.
*   **`DevPilotAI.Application`**: Core Business Logic interfaces, request validations (FluentValidation), object mapping (AutoMapper), and assembly-wide dependency injections.
*   **`DevPilotAI.Domain`**: Core Enterprise Entities. Defines base entities, auditable fields, and soft-delete contracts.
*   **`DevPilotAI.Infrastructure`**: Implementation layer. Provides concrete adapters such as `DateTimeProvider`.
*   **`DevPilotAI.Shared`**: Common cross-cutting models like functional `Result`, standard API response wrappers (`ApiResponse`), and pagination metadata (`PagedResult`, `PaginationRequest`).
*   **`DevPilotAI.UnitTests`**: Target unit testing suite.

---

## 🚀 Getting Started

### Prerequisites

*   **.NET 10 SDK** (Installed SDK: `10.0.203` or newer)
*   **Docker Desktop** (Optional, for containerized database/dependencies)

### Local Configuration

API launch variables and port bindings are declared in `DevPilotAI.Api/Properties/launchSettings.json`.

*   **HTTP**: `http://localhost:5199`
*   **HTTPS**: `https://localhost:7136`

### Building and Running the Solution

Restore dependencies and build the solution:

```bash
dotnet restore
dotnet build
```

Run the Web API project:

```bash
dotnet run --project DevPilotAI.Api/DevPilotAI.Api.csproj
```

Once running, you can access the developer interface and diagnostic endpoints:
*   **Swagger API Docs**: `http://localhost:5199/swagger`
*   **Health Checks Endpoint**: `http://localhost:5199/health` (Returns structured JSON diagnostics)

---

## 🐳 Docker Deployment

To build and run the platform using Docker Compose:

```bash
docker-compose up --build
```

This launches the Web API along with **Qdrant** (Vector Database), which handles the semantic indexing.
- API is exposed at `http://localhost:5199`
- Qdrant dashboard/REST API at `http://localhost:6333`
