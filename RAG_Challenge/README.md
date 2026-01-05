# RAG Challenge

This project implements a RAG (Retrieval-Augmented Generation) orchestration service.

---
**For Portuguese (PT-BR), please scroll down.**
**Para Português do Brasil, por favor role para baixo.**
---

## English 🇺🇸

### How to Run

#### Docker

To build and run the application using Docker, follow these steps from the root of the solution:

1.  **Build the Docker image:**

    ```bash
    docker build -t rag-challenge:local -f RAG_Challenge/Dockerfile .
    ```

2.  **Run the container:**
    
    You need to provide the necessary environment variables. Replace the placeholders with your actual API keys.

    ```bash
    docker run --rm -p 8080:8080 \
      -e ASPNETCORE_ENVIRONMENT=Development \
      -e ExternalApis__OpenAI__BaseUrl="https://api.openai.com/v1/" \
      -e ExternalApis__OpenAI__ApiKey="YOUR_OPENAI_API_KEY" \
      -e ExternalApis__VectorDb__BaseUrl="https://claudia-db.search.windows.net/" \
      -e ExternalApis__VectorDb__ApiKey="YOUR_VECTORDB_API_KEY" \
      -e ExternalApis__VectorDb__IndexName="claudia-ids-index-large" \
      -e ExternalApis__VectorDb__ApiVersion="2023-11-01" \
      rag-challenge:local
    ```

#### Docker Compose

To run using Docker Compose:

1.  Ensure you have a `docker-compose.yml` file configured and the environments variables set in you environment.
2.  You may need to create a `.env` file or modify the `docker-compose.yml` to include your API keys.
3.  Run the following command:

    ```bash
    docker-compose up --build
    ```

#### Running from IDE (Rider / Visual Studio)

If you prefer to run the project directly from your IDE (like JetBrains Rider or Visual Studio), you must configure the `launchSettings.json` file located in `RAG_Challenge/Properties/launchSettings.json`.

Update the `http` profile to include the required environment variables as shown below:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5187",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ExternalApis__OpenAI__BaseUrl": "https://api.openai.com/v1/",
        "ExternalApis__OpenAI__ApiKey": "YOUR_OPENAI_API_KEY",
        "ExternalApis__VectorDb__BaseUrl": "https://claudia-db.search.windows.net/",
        "ExternalApis__VectorDb__ApiKey": "YOUR_VECTORDB_API_KEY",
        "ExternalApis__VectorDb__IndexName": "claudia-ids-index-large",
        "ExternalApis__VectorDb__ApiVersion": "2023-11-01"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7179;http://localhost:5187",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Running Tests

The project includes integration/unit tests in the `RAG_Challenge.Tests` project. 

**Important:** The tests require valid API keys to run successfully against the real services (in the "RealFlow" tests). You must manually replace the placeholders in `RagOrchestratorTests.cs` with your actual keys before running them.

```csharp
private const string OpenAiApiKey = "PLACEHOLDER"; // Replace with actual key
private const string VectorDbApiKey = "PLACEHOLDER"; // Replace with actual key
```

---

## Português (PT-BR) 🇧🇷

### Como Executar

#### Docker

Para compilar e executar a aplicação usando Docker, siga estes passos a partir da raiz da solução:

1.  **Construir a imagem Docker:**

    ```bash
    docker build -t rag-challenge:local -f RAG_Challenge/Dockerfile .
    ```

2.  **Executar o container:**
    
    Você precisa fornecer as variáveis de ambiente necessárias. Substitua os placeholders pelas suas chaves de API reais.

    ```bash
    docker run --rm -p 8080:8080 \
      -e ASPNETCORE_ENVIRONMENT=Development \
      -e ExternalApis__OpenAI__BaseUrl="https://api.openai.com/v1/" \
      -e ExternalApis__OpenAI__ApiKey="SUA_CHAVE_API_OPENAI" \
      -e ExternalApis__VectorDb__BaseUrl="https://claudia-db.search.windows.net/" \
      -e ExternalApis__VectorDb__ApiKey="SUA_CHAVE_API_VECTORDB" \
      -e ExternalApis__VectorDb__IndexName="claudia-ids-index-large" \
      -e ExternalApis__VectorDb__ApiVersion="2023-11-01" \
      rag-challenge:local
    ```

#### Docker Compose

Para executar usando Docker Compose:

1.  Certifique-se de ter as variáveis de ambiente definidas no seu terminal ou em um arquivo `.env` no mesmo diretório.
2.  Execute o seguinte comando:

    ```bash
    docker-compose up --build
    ```

#### Executando pela IDE (Rider / Visual Studio)

Se você preferir executar o projeto diretamente da sua IDE (como JetBrains Rider ou Visual Studio), você deve configurar o arquivo `launchSettings.json` localizado em `RAG_Challenge/Properties/launchSettings.json`.

Atualize o perfil `http` para incluir as variáveis de ambiente necessárias, conforme mostrado abaixo:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5187",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ExternalApis__OpenAI__BaseUrl": "https://api.openai.com/v1/",
        "ExternalApis__OpenAI__ApiKey": "SUA_CHAVE_API_OPENAI",
        "ExternalApis__VectorDb__BaseUrl": "https://claudia-db.search.windows.net/",
        "ExternalApis__VectorDb__ApiKey": "SUA_CHAVE_API_VECTORDB",
        "ExternalApis__VectorDb__IndexName": "claudia-ids-index-large",
        "ExternalApis__VectorDb__ApiVersion": "2023-11-01"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7179;http://localhost:5187",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Executando Testes

O projeto inclui testes de integração/unidade no projeto `RAG_Challenge.Tests`.

**Importante:** Os testes requerem chaves de API válidas para rodar com sucesso contra os serviços reais (nos testes "RealFlow"). Você deve substituir manualmente os placeholders em `RagOrchestratorTests.cs` pelas suas chaves reais antes de executá-los.

```csharp
private const string OpenAiApiKey = "PLACEHOLDER"; // Substitua pela chave real
private const string VectorDbApiKey = "PLACEHOLDER"; // Substitua pela chave real
```

