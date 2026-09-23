# Aygaz E-Commerce AI Agent

A C#/.NET e-commerce AI agent demo with a local data layer, guardrails, RAG document search, and a multi-agent Semantic Kernel chat flow.

The project runs entirely on demo/synthetic data. It does not use real Aygaz customer, order, product, inventory, price, sales, or policy data.

## Features

- .NET 9 console app, ASP.NET Core Web API, and vanilla Chat UI
- Customer, order, product, inventory, sales, and support-policy agents through Semantic Kernel
- Central Aygaz domain guardrail and capability-based routing
- MongoDB as the default data layer; SQLite and SQL Server alternatives through Dapper
- Controlled order status update and cancellation flows
- Persistent MongoDB-backed RAG index and optional local in-memory RAG
- Semantic Kernel provider selection for Ollama or Groq/OpenAI-compatible endpoints
- Lexical embedding by default; optional Ollama `nomic-embed-text`
- Temporary AI provider error classification, retry handling, and deterministic fast-path fallbacks
- xUnit coverage for data access, routing, guardrails, RAG, and Web API contracts

## Requirements

- .NET 9 SDK
- MongoDB (`localhost:27017` by default)
- Optional: Ollama
- Optional: SQLite or SQL Server
- xUnit test runner

Recommended Ollama models:

```powershell
ollama pull qwen3:4b-instruct
ollama pull nomic-embed-text
```

If you use Groq or OpenAI, provide the related API key as an environment variable:

```powershell
$env:GROQ_API_KEY = "..."
$env:OPENAI_API_KEY = "..."
```

## Quick Start

Restore, build, and test from the repository root:

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

Run the main Semantic Kernel Web Chat demo:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.SemanticKernel.Web\Aygaz.ECommerce.SemanticKernel.Web.csproj
```

Open in a browser:

```text
http://localhost:5190
```

Seed MongoDB demo data on first setup or when you want to refresh the dataset:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.SemanticKernel.Web\Aygaz.ECommerce.SemanticKernel.Web.csproj -- --seed-mongodb
```

Manually rebuild the RAG document index:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.SemanticKernel.Web\Aygaz.ECommerce.SemanticKernel.Web.csproj -- --ingest-rag
```

Run the console demo:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.Agent\Aygaz.ECommerce.Agent.csproj
```

## Project Structure

```text
src/
  Aygaz.AgentFramework/                  Agent registration, execution, telemetry, and retry infrastructure
  Aygaz.ECommerce.Agent/                 Data access, services, tools, guardrails, and RAG
  Aygaz.ECommerce.SemanticKernel/        Semantic Kernel agents, plugins, and chat orchestration
  Aygaz.ECommerce.SemanticKernel.Web/    Main Web API + Chat UI demo
  Aygaz.ECommerce.Web/                   Alternative Ollama/native-agent Web API
  Aygaz.ECommerce.SemanticKernel.Demo/   Minimal Semantic Kernel console demo
tests/
  *.Tests/                               Unit and API contract tests
data/
  demo-documents/                        Synthetic support-policy documents
```

## Configuration

Default settings live in the project `appsettings.json` files. The most important sections are:

```json
{
  "DataAccess": {
    "Provider": "MongoDb",
    "MongoDb": {
      "ConnectionString": "mongodb://localhost:27017",
      "DatabaseName": "aygaz_ecommerce_demo"
    }
  },
  "SemanticKernel": {
    "Provider": "Groq",
    "ModelId": "openai/gpt-oss-20b",
    "Endpoint": "https://api.groq.com/openai/v1"
  },
  "Rag": {
    "StoreProvider": "MongoDb",
    "EmbeddingProvider": "Lexical",
    "DocumentsPath": "data/demo-documents",
    "MaxRetrievalResults": 3,
    "MaxQueryLength": 500,
    "MinimumSimilarityScore": 0.15,
    "AutoIngestOnStartup": true
  }
}
```

`DataAccess:Provider` can be `MongoDb`, `Sqlite`, or `SqlServer`. MongoDB is the default.

`Rag:StoreProvider`:

- `MongoDb`: stores document chunks and embeddings persistently in MongoDB.
- `Local`: indexes documents in memory inside the application.

`Rag:EmbeddingProvider`:

- `Lexical`: deterministic lexical embedding with no extra model requirement.
- `Ollama`: local Ollama embedding service through `Ollama:EmbeddingModel`.

## Architecture Flow

Every user message passes through the domain guardrail first. If the guardrail allows the request, the capability is resolved and the related Semantic Kernel agent runs.

```text
Browser / Console
  -> Chat API / Chat Service
  -> Aygaz Domain Guardrail
  -> Capability Resolver
  -> Semantic Kernel Agent
  -> Plugin / C# Service
  -> MongoDB, SQL, or RAG
  -> final answer
```

Business agents are not invoked for out-of-scope or ambiguous requests. This boundary is enforced in the C# service flow, not only through prompts.

## Domain Guardrail

User input does not go directly to a business agent. It is evaluated by a separate guardrail layer first:

```text
User input
  -> DomainGuardrail
     -> Allowed    -> related agent and tool flow
     -> OutOfScope -> fixed out-of-scope response
     -> Ambiguous  -> fixed clarification response
```

Decisions:

- `Allowed`: customer, order, product, inventory, sales, or support-policy requests in the Aygaz e-commerce demo scope
- `OutOfScope`: requests about other organizations or topics outside the e-commerce domain
- `Ambiguous`: messages where the Aygaz e-commerce connection cannot be determined reliably

After the guardrail decision, the capability resolver selects the related capability. Policy, order, product, inventory, customer, and sales boundaries are kept independent. For example, another company's return policy remains `OutOfScope`, while the Aygaz demo return policy is routed to the support-policy RAG flow.

Examples:

```text
Allowed:    Who is the customer ahmet.yilmaz@example.com?
Allowed:    What is the status of order AYG-DEMO-1001?
Allowed:    Is AYG-DEMO-PRD-001 in stock?
Allowed:    What is the return period?
OutOfScope: What is Arcelik's return policy?
OutOfScope: Tell me today's football scores.
Ambiguous:  Check its status.
```

## Agents and Capabilities

- Customer: customer lookup by ID, email, and full-name search
- Order: order details, customer orders, latest order, status update, and cancellation
- Product: product lookup by SKU, name, and category
- Inventory: product stock and location information
- Sales: sales summary, top-selling products, and customer purchase summary
- Support Policy: return, delivery, campaign, and support documents

Raw entity graphs are not exposed outside the service layer. Tool responses are limited to DTOs and computed summaries.

## Tool Allow-List

Agents can only call controlled service methods. There are no reflection, raw SQL, export, broad list-all, or unlimited data-dump tools.

Customer:

- `get_customer_by_email(email)`
- `get_customer_by_id(id)`
- `search_customers_by_name(query)`

Order:

- `get_order_by_number(orderNumber)`
- `get_customer_orders(customerId)`
- `get_latest_customer_order(customerId)`
- `update_order_status(orderNumber, status, reason)`
- `cancel_order(orderNumber, reason)`

Product:

- `get_product_by_sku(sku)`
- `search_products(query)`

Inventory:

- `get_product_inventory(productId)`
- `get_total_product_stock(productId)`

Sales:

- `get_sales_summary(fromDate, toDate)`
- `get_top_selling_products(fromDate, toDate, limit)`
- `get_customer_purchase_summary(customerId, fromDate, toDate)`

Support Policy:

- `search_documents(query)`

`get_all_customers`, `get_all_orders`, `get_all_products`, `get_all_inventory`, raw entity graphs, raw SQL, price mutation, stock mutation, refund, and export tools are intentionally not exposed.

## Multi-Tool Flows

Semantic Kernel agents can use multiple tools within a single answer. C# does not force the next tool through keyword routing; the agent continues by using IDs or context returned from previous tool calls.

Customer -> Order:

```text
User -> search_customers_by_name -> Customer ID
     -> get_latest_customer_order
     -> final answer
```

Product -> Inventory:

```text
User -> get_product_by_sku -> Product ID
     -> get_total_product_stock or get_product_inventory
     -> final answer
```

Customer -> Order -> Policy:

```text
User -> search_customers_by_name -> Customer ID
     -> get_latest_customer_order
     -> search_documents
     -> final answer
```

If the provider is temporarily unavailable, supported deterministic fast paths can take over. For example, exact order-number, SKU, or explicit policy requests can be answered through service/RAG results.

## RAG / Document Search

Demo documents:

- `data/demo-documents/return-policy.txt`
- `data/demo-documents/delivery-policy.txt`
- `data/demo-documents/campaign-policy.txt`
- `data/demo-documents/customer-support.txt`

In MongoDB mode, if `AutoIngestOnStartup=true`, documents are chunked, embedded, and upserted into the `documentChunks` collection on application startup. Search only provides the most relevant limited chunks as context for the agent answer.

RAG flow:

```text
Documents
  -> paragraph chunks
  -> Lexical or Ollama embedding
  -> MongoDB documentChunks or in-memory index
  -> cosine similarity
  -> up to 3 most relevant chunks
  -> support-policy agent response
```

The default `Lexical` embedding requires no extra model and behaves deterministically in tests. If `Ollama` embedding is selected, a local embedding model such as `nomic-embed-text` is used.

Examples:

```text
What is the return period?
Check Ahmet Yilmaz's latest order and explain the return policy.
```

## Web API and Chat UI

The main demo is the `Aygaz.ECommerce.SemanticKernel.Web` project. It exposes the existing service, guardrail, agent, and RAG layers through an HTTP API and static chat UI without rewriting them.

Endpoints:

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Basic health check |
| `POST /api/chat` | Chat through the guardrail and Semantic Kernel agents |
| `POST /api/chat/clear` | Clears session conversation history |

`POST /api/chat` request:

```json
{
  "message": "What is Ahmet Yilmaz's latest order?",
  "sessionId": "optional-browser-session-id"
}
```

Response:

```json
{
  "success": true,
  "message": "...",
  "scope": "Allowed",
  "sessionId": "..."
}
```

The API does not return raw tool payloads, raw LLM responses, or entity graphs. In-memory conversation context is preserved through the browser session ID.

## Data Layer and Demo Seed

MongoDB is the default data layer. `DataAccess:Provider` can select `MongoDb`, `Sqlite`, or `SqlServer`.

Demo data properties:

- Customer, order, order item, product, inventory, and order audit log records are synthetic.
- Emails use `example.com`, orders use `AYG-DEMO-*`, and SKUs use the `AYG-DEMO-PRD-*` format.
- Mongo seed only runs with `--seed-mongodb` and uses fixed keys with upsert behavior, so reruns do not create duplicates.
- Normal Mongo startup prepares collections and indexes; writing seed data requires the explicit seed command.
- The `documentChunks` collection stores RAG chunks and embedding records.
- When a relational provider is selected, the Dapper initializer prepares the table schema; it is not a replacement for a production migration process.

Sales analytics services return computed DTOs instead of raw order item lists. Cancelled orders are excluded from aggregate calculations.

## Example Questions

```text
Who is ahmet.yilmaz@example.com?
Find the customer named Ahmet Yilmaz.
Get customer number 1.
What is the status of order AYG-DEMO-1001?
Mark order AYG-DEMO-1001 as delivered.
Cancel order AYG-DEMO-1002.
Find product AYG-DEMO-PRD-001.
Is AYG-DEMO-PRD-003 in stock?
Is Demo Product Alpha in stock?
Show the e-commerce sales summary for the last 30 days.
Show the top 5 products sold in the last 90 days.
How much did Ahmet Yilmaz spend in the last 90 days?
What is the return period?
Check Ahmet Yilmaz's latest order and explain the return policy.
What is Arcelik's return policy?
```

## Test Coverage

Tests are designed to avoid depending on a local LLM. Coverage includes:

- Dapper/SQLite data access contracts
- Mongo provider selection, index, and seed contracts
- Lexical embedding and document retrieval behavior
- Semantic Kernel routing, fast-path, and follow-up behavior
- Guardrail fail-closed decisions
- Web API validation, session, and error responses
- AI provider retry/fallback behavior

Run:

```powershell
dotnet test .\Aygaz.ECommerce.Agent.sln
```

## Safety and Boundaries

- The dataset is deterministic and synthetic.
- Authentication/authorization is not production-ready in this demo scope.
- The guardrail answers out-of-scope requests without invoking the agent/tool layer.
- Mongo seed only runs manually with `--seed-mongodb`.
- RAG documents are not real internal company documents.
- Supported deterministic fast paths can respond when the LLM provider has temporary failures.
