# RAG Challenge – Flow & Debug Guide

## High-level flow
- Entry point: `Program.cs` exposes a single endpoint `POST /rag/ask` that binds a JSON `RagRequest` (question + optional chat history).
- Composition: `builder.Services.AddInfrastructure(...)` wires DI for:
  - `OpenAiHttpClient` (embeddings + chat completions)
  - `VectorDbHttpClient` (Azure Search vector query)
  - `RagOrchestrator` (chains the steps below)
- Request pipeline in `RagOrchestrator.GenerateAnswerAsync`:
  1) **Embed question** via OpenAI `POST /embeddings` (model `text-embedding-3-large`).
  2) **Vector search** in Azure Search index using the returned vector (`VectorSearchRequest` with `k=3`, `top=10`, filter `projectName eq 'tesla_motors'`).
  3) **Assemble chat messages**: system prompt + prior `history` + current question + retrieved context snippets.
  4) **Chat completion** via OpenAI `POST /chat/completions` (model `gpt-4o`).
  5) **Response**: `ChatOrchestrationResult` containing the answer, embedding payload, retrieved chunks, and raw completion.

## Key types and where to look
- Contracts: `RAG_Challenge.Domain/Contracts` (`IOpenAiClient`, `IVectorDbClient`, `IRagOrchestrator`).
- Models: `RAG_Challenge.Domain/Models/*` (chat, embeddings, vector search, RAG request/result).
- Infrastructure:
  - Config: `RAG_Challenge.Infrastructure/Configuration` (`OpenAiOptions`, `VectorDbOptions`).
  - Clients: `RAG_Challenge.Infrastructure/Clients` (`OpenAiHttpClient`, `VectorDbHttpClient`).
  - Orchestration: `RAG_Challenge.Infrastructure/Orchestration/RagOrchestrator.cs`.
  - DI: `RAG_Challenge.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs`.
- API host: `RAG_Challenge/Program.cs`.

## Request contract
```json
{
  "question": "How long does a Tesla battery last before it needs to be replaced?",
  "history": [
    { "role": "user", "content": "Hi" },
    { "role": "assistant", "content": "Hello!" }
  ]
}
```

## Running & debugging locally (JetBrains Rider/VS/VS Code or CLI)
1) Set required env vars (example using PowerShell):
```powershell
$env:ExternalApis__OpenAI__BaseUrl="https://api.openai.com/v1/"
$env:ExternalApis__OpenAI__ApiKey="<OPENAI_KEY>"
$env:ExternalApis__VectorDb__BaseUrl="https://claudia-db.search.windows.net/"
$env:ExternalApis__VectorDb__ApiKey="<AZURE_SEARCH_KEY>"
$env:ExternalApis__VectorDb__IndexName="claudia-ids-index-large"
$env:ExternalApis__VectorDb__ApiVersion="2023-11-01"
```

2) Run in debug:
- **Rider/VS**: set `RAG_Challenge` as startup project, launch with Debug (env vars above in Run Configuration / launchSettings override or Env vars tab).
- **VS Code**: use a `.vscode/launch.json` `.NET` profile that runs `project`: `RAG_Challenge/RAG_Challenge.csproj` and add env vars.
- **CLI quick run**:
```powershell
cd C:\Users\Gustavo\Documents\Projetos\RAG_Challenge\RAG_Challenge
-dotnet run --project RAG_Challenge/RAG_Challenge.csproj
```

3) Hit the endpoint (Postman/curl):
```bash
curl -X POST http://localhost:8080/rag/ask \
  -H "Content-Type: application/json" \
  -d '{"question":"How long does a Tesla battery last?","history":[]}'
```

## Where to set breakpoints to watch the flow
- `RAG_Challenge/Program.cs`: inside the `/rag/ask` handler to see DI resolution and request binding.
- `RAG_Challenge.Infrastructure/Orchestration/RagOrchestrator.cs`: inside `GenerateAnswerAsync` at each step (embed, search, chat completion).
- `RAG_Challenge.Infrastructure/Clients/OpenAiHttpClient.cs`: before `SendAsync` for embeddings/chat.
- `RAG_Challenge.Infrastructure/Clients/VectorDbHttpClient.cs`: before `SendAsync` for vector search.

## Typical issues to check while debugging
- Missing env vars → 401 from OpenAI/Azure Search.
- Empty embedding result → `Embedding failed` guard triggers.
- Vector search returning empty → check index name/api-version/filter and vector dimensions.
- Chat completion errors → verify OpenAI model name and quota.

