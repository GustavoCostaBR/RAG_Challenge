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

### Technical Decisions & Trade-offs

*   **AI Stability & Retries:** To mitigate potential AI hallucinations—specifically cases where the model might incorrectly flag content as "N2" (requiring human escalation) or fail to parse responses—I implemented retry logic. This ensures that transient errors or minor inconsistencies in the model's output don't immediately lead to failure or unnecessary escalation.
    *   **Optimization:** I added a check to verify if any retrieved content is actually labeled "N2". If the model requests escalation but no "N2" content exists in the context, it is treated as a hallucination, and the retry logic is skipped to save time.
    *   **Trade-off:** For valid escalation cases (where "N2" content is present), the process might take slightly longer due to the retry loop (configured via `maxLogicRetries`). This is an acceptable trade-off because "N2" content is expected to be rare, and ensuring the necessity of escalation is prioritized over speed in these specific cases.
*   **Error Handling:** I adopted a `Result<T>` wrapper pattern instead of relying on exceptions for flow control. This provides more granular control over error states and makes the data flow more explicit.
*   **Testing Strategy:** The project includes basic flow tests to verify the main execution paths and ensure regressions weren't introduced during refactoring. However, comprehensive edge case coverage was not the primary focus for this iteration.
*   **Heuristics:** The threshold values used for heuristics (e.g., determining when to ask for clarification based on vector search scores) are currently estimated. In a production environment, these would need to be tuned based on real usage data and more extensive testing.
*   **Coverage Evaluation:** The process of evaluating whether the retrieved context is sufficient to answer the question has been isolated into a separate AI request. This "Coverage Judge" step improves the quality of the final answer by filtering out irrelevant context early.
*   **Architecture:** The architecture was kept intentionally simple to focus on the challenge's core problems. While it follows good practices (dependency injection, separation of concerns), it avoids over-engineering.
*   **Project Management:** Project configurations (like the Tesla project ID) are currently stored in an in-memory dictionary. A production-ready solution would persist this data in a database to allow for dynamic updates without recompilation.
*   **Clarification Tagging:** For simplicity, clarification requests are marked with a `[clarification]` string tag in the response. This allows for easy parsing by the client, though a more structured approach (e.g., a specific JSON field) could be considered for the future.
*   **Scope:** Security implementation and extensive logging were considered out of scope for this challenge to prioritize the core RAG logic and feature implementation.

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

### Decisões Técnicas e Compromissos

*   **Estabilidade da IA e Novas Tentativas:** Para mitigar potenciais alucinações da IA—especificamente casos onde o modelo pode sinalizar incorretamente o conteúdo como "N2" (exigindo escalonamento humano) ou falhar ao analisar respostas—implementei uma lógica de nova tentativa. Isso garante que erros transitórios ou pequenas inconsistências na saída do modelo não levem imediatamente à falha ou escalonamento desnecessário.
    *   **Otimização:** Adicionei uma verificação para confirmar se algum conteúdo recuperado é realmente rotulado como "N2". Se o modelo solicitar escalonamento, mas não houver conteúdo "N2" no contexto, isso é tratado como uma alucinação e a lógica de nova tentativa é ignorada para economizar tempo.
    *   **Compromisso:** Para casos de escalonamento válidos (onde o conteúdo "N2" está presente), o processo pode demorar um pouco mais devido ao loop de novas tentativas (configurado via `maxLogicRetries`). Este é um compromisso aceitável porque espera-se que o conteúdo "N2" seja raro, e garantir a necessidade de escalonamento é priorizado em relação à velocidade nesses casos específicos.
*   **Tratamento de Erros:** Adotei um padrão de wrapper `Result<T>` em vez de depender de exceções para controle de fluxo. Isso fornece um controle mais granular sobre os estados de erro e torna o fluxo de dados mais explícito.
*   **Estratégia de Testes:** O projeto inclui testes de fluxo básicos para verificar os principais caminhos de execução e garantir que regressões não sejam introduzidas durante a refatoração. No entanto, a cobertura abrangente de casos extremos não foi o foco principal desta iteração.
*   **Heurísticas:** Os valores de limiar usados para heurísticas (por exemplo, determinar quando pedir esclarecimentos com base nas pontuações de busca vetorial) são atualmente estimados. Em um ambiente de produção, estes precisariam ser ajustados com base em dados de uso reais e testes mais extensos.
*   **Avaliação de Cobertura:** O processo de avaliação se o contexto recuperado é suficiente para responder à pergunta foi isolado em uma solicitação de IA separada. Esta etapa "Juiz de Cobertura" melhora a qualidade da resposta final filtrando contextos irrelevantes precocemente.
*   **Arquitetura:** A arquitetura foi mantida intencionalmente simples para focar nos problemas centrais do desafio. Embora siga boas práticas (injeção de dependência, separação de preocupações), evita superengenharia.
*   **Gerenciamento de Projetos:** As configurações do projeto (como o ID do projeto Tesla) estão atualmente armazenadas em um dicionário na memória. Uma solução pronta para produção persistiria esses dados em um banco de dados para permitir atualizações dinâmicas sem recompilação.
*   **Tagueamento de Esclarecimento:** Para simplicidade, os pedidos de esclarecimento são marcados com uma tag de string `[clarification]` na resposta. Isso permite uma fácil análise pelo cliente, embora uma abordagem mais estruturada (por exemplo, um campo JSON específico) possa ser considerada para o futuro.
*   **Escopo:** A implementação de segurança e o registro extensivo foram considerados fora do escopo deste desafio para priorizar a lógica central do RAG e a implementação de recursos.
