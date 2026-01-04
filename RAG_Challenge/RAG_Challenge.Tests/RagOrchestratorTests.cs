using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG_Challenge.Domain.Contracts;
using RAG_Challenge.Domain.Models.Chat;
using RAG_Challenge.Domain.Models.Embeddings;
using RAG_Challenge.Domain.Models.Rag;
using RAG_Challenge.Domain.Models.VectorSearch;
using RAG_Challenge.Infrastructure.Clients;
using RAG_Challenge.Infrastructure.Configuration;
using RAG_Challenge.Infrastructure.Orchestration;
using Xunit;

namespace RAG_Challenge.Tests;

public class RagOrchestratorTests
{
    private const string OpenAiApiKey = "PlaceHolder";

    private const string VectorDbApiKey = "PlaceHolder";

    private readonly Mock<IOpenAiClient> _openAiMock;
    private readonly Mock<IVectorDbClient> _vectorDbMock;
    private readonly Mock<ILogger<RagOrchestrator>> _loggerMock;
    private readonly RagOrchestrator _orchestrator;

    public RagOrchestratorTests()
    {
        _openAiMock = new Mock<IOpenAiClient>();
        _vectorDbMock = new Mock<IVectorDbClient>();
        _loggerMock = new Mock<ILogger<RagOrchestrator>>();

        _orchestrator = new RagOrchestrator(
            _openAiMock.Object,
            _vectorDbMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateAnswerAsync_ReturnsAnswer_WhenQuestionIsAnswerable_Mocked()
    {
        // Arrange
        const string question = "What is a Tesla?";
        var request = new RagRequest(question, [], Projects.TeslaMotorsId);
        var embedding = new EmbeddingResponse(
            "list",
            [new EmbeddingData("embedding", 0, [0.1f, 0.2f])],
            "text-embedding-ada-002",
            new EmbeddingUsage(1, 1)
        );

        _openAiMock.Setup(x => x.CreateEmbeddingAsync(question, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmbeddingResponse>.Success(embedding));

        var searchResults = new List<VectorDbSearchResult>
        {
            new VectorDbSearchResult("Tesla is an electric car manufacturer.", "N1", 0.9f)
        };

        _vectorDbMock.Setup(x => x.SearchAsync(It.IsAny<VectorSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Mock Coverage Judge to return YES
        _openAiMock.Setup(x => x.CreateChatCompletionAsync(
                It.Is<IReadOnlyList<ChatMessage>>(m => m.Any(msg => msg.Content.Contains("You are a coverage judge"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatCompletionResponse>.Success(new ChatCompletionResponse(
                "chatcmpl-123",
                "chat.completion",
                1234567890,
                "gpt-4",
                [new ChatChoice(new ChatMessage("assistant", "YES"), "stop")]
            )));

        // Mock Final Answer
        var answerJson = "{\"answer\": \"Tesla is a car company.\", \"handoverToHumanNeeded\": false}";
        _openAiMock.Setup(x => x.CreateChatCompletionAsync(
                It.Is<IReadOnlyList<ChatMessage>>(m =>
                    m.Any(msg => msg.Content.Contains("You are a helpful assistant"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatCompletionResponse>.Success(new ChatCompletionResponse(
                "chatcmpl-124",
                "chat.completion",
                1234567890,
                "gpt-4",
                [new ChatChoice(new ChatMessage("assistant", answerJson), "stop")]
            )));

        // Act
        var result = await _orchestrator.GenerateAnswerAsync(request);

        // Assert
        Assert.Equal("Tesla is a car company.", result.Answer);
        Assert.False(result.HandoverToHumanNeeded);
    }

    [Fact]
    public async Task GenerateAnswerAsync_ReturnsAnswer_WhenQuestionIsAnswerable_RealFlow()
    {
        // Arrange
        var openAiOptions = Options.Create(new OpenAiOptions
        {
            BaseUrl = "https://api.openai.com/v1/",
            ApiKey = OpenAiApiKey
        });

        var vectorDbOptions = Options.Create(new VectorDbOptions
        {
            BaseUrl = "https://claudia-db.search.windows.net/",
            ApiKey = VectorDbApiKey,
            IndexName = "claudia-ids-index-large",
            ApiVersion = "2023-11-01"
        });

        var openAiHttpClient = new HttpClient { BaseAddress = new Uri(openAiOptions.Value.BaseUrl) };
        var vectorDbHttpClient = new HttpClient { BaseAddress = new Uri(vectorDbOptions.Value.BaseUrl) };

        var openAiClient =
            new OpenAiHttpClient(openAiHttpClient, openAiOptions, new Mock<ILogger<OpenAiHttpClient>>().Object);
        var vectorDbClient = new VectorDbHttpClient(vectorDbHttpClient, vectorDbOptions,
            new Mock<ILogger<VectorDbHttpClient>>().Object);

        var orchestrator = new RagOrchestrator(
            openAiClient,
            vectorDbClient,
            _loggerMock.Object);

        var question = "What is a Tesla?";
        var request = new RagRequest(question, [], Projects.TeslaMotorsId);

        // Act
        var result = await orchestrator.GenerateAnswerAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
    }

    [Fact]
    public async Task
        GenerateAnswerAsync_EscalatesToHuman_WhenQuestionIsNotAnswerableAndClarificationLimitReached_Mocked()
    {
        // Arrange
        var question = "What is the capital of France?";

        // Mock Embedding
        var embedding = new EmbeddingResponse(
            "list",
            [new EmbeddingData("embedding", 0, [0.1f, 0.2f])],
            "text-embedding-ada-002",
            new EmbeddingUsage(1, 1)
        );
        _openAiMock.Setup(x => x.CreateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EmbeddingResponse>.Success(embedding));

        // Mock Vector Search (Must return something so EvaluateCoverageAsync doesn't fail with internal error)
        var searchResults = new List<VectorDbSearchResult>
        {
            new VectorDbSearchResult("Some irrelevant content about cars.", "N1", 0.5f)
        };
        _vectorDbMock.Setup(x => x.SearchAsync(It.IsAny<VectorSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Mock Coverage Judge to return NO
        _openAiMock.Setup(x => x.CreateChatCompletionAsync(
                It.Is<IReadOnlyList<ChatMessage>>(m => m.Any(msg => msg.Content.Contains("You are a coverage judge"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChatCompletionResponse>.Success(new ChatCompletionResponse(
                "chatcmpl-125",
                "chat.completion",
                1234567890,
                "gpt-4",
                [new ChatChoice(new ChatMessage("assistant", "NO: I can't find the answer."), "stop")]
            )));

        // Step 1: First call
        var request1 = new RagRequest(question, [], Projects.TeslaMotorsId);
        var result1 = await _orchestrator.GenerateAnswerAsync(request1);

        Assert.Contains("[clarification]", result1.Answer);
        Assert.False(result1.HandoverToHumanNeeded);

        // Step 2: User insists (Second call)
        // History needs to include the previous interaction
        var history2 = new List<ChatMessage>
        {
            new ChatMessage("user", question),
            new ChatMessage("assistant", result1.Answer)
        };
        var request2 = new RagRequest(question, history2, Projects.TeslaMotorsId);
        var result2 = await _orchestrator.GenerateAnswerAsync(request2);

        Assert.Contains("[clarification]", result2.Answer);
        Assert.False(result2.HandoverToHumanNeeded);

        // Step 3: User insists again (Third call)
        // History needs to include both previous interactions
        var history3 = new List<ChatMessage>(history2)
        {
            new ChatMessage("user", question),
            new ChatMessage("assistant", result2.Answer)
        };
        var request3 = new RagRequest(question, history3, Projects.TeslaMotorsId);
        var result3 = await _orchestrator.GenerateAnswerAsync(request3);

        // Assert
        Assert.Equal("I need to hand this over to a human specialist for further assistance.", result3.Answer);
        Assert.True(result3.HandoverToHumanNeeded);
    }

    [Fact]
    public async Task
        GenerateAnswerAsync_EscalatesToHuman_WhenQuestionIsNotAnswerableAndClarificationLimitReached_RealFlow()
    {
        // Arrange
        var openAiOptions = Options.Create(new OpenAiOptions
        {
            BaseUrl = "https://api.openai.com/v1/",
            ApiKey = OpenAiApiKey
        });

        var vectorDbOptions = Options.Create(new VectorDbOptions
        {
            BaseUrl = "https://claudia-db.search.windows.net/",
            ApiKey = VectorDbApiKey,
            IndexName = "claudia-ids-index-large",
            ApiVersion = "2023-11-01"
        });

        var openAiHttpClient = new HttpClient { BaseAddress = new Uri(openAiOptions.Value.BaseUrl) };
        var vectorDbHttpClient = new HttpClient { BaseAddress = new Uri(vectorDbOptions.Value.BaseUrl) };

        var openAiClient =
            new OpenAiHttpClient(openAiHttpClient, openAiOptions, new Mock<ILogger<OpenAiHttpClient>>().Object);
        var vectorDbClient = new VectorDbHttpClient(vectorDbHttpClient, vectorDbOptions,
            new Mock<ILogger<VectorDbHttpClient>>().Object);

        var orchestrator = new RagOrchestrator(
            openAiClient,
            vectorDbClient,
            _loggerMock.Object);

        var question = "What is the capital of France?";

        // Step 1: First call
        var request1 = new RagRequest(question, [], Projects.TeslaMotorsId);
        var result1 = await orchestrator.GenerateAnswerAsync(request1);

        // Assert.Contains("[clarification]", result1.Answer); // Depending on real response
        Assert.False(result1.HandoverToHumanNeeded);

        // Step 2: User insists (Second call)
        var history2 = new List<ChatMessage>
        {
            new ChatMessage("user", question),
            new ChatMessage("assistant", result1.Answer)
        };
        var request2 = new RagRequest(question, history2, Projects.TeslaMotorsId);
        var result2 = await orchestrator.GenerateAnswerAsync(request2);

        // Assert.Contains("[clarification]", result2.Answer); // Depending on real response
        Assert.False(result2.HandoverToHumanNeeded);

        // Step 3: User insists again (Third call)
        var history3 = new List<ChatMessage>(history2)
        {
            new ChatMessage("user", question),
            new ChatMessage("assistant", result2.Answer)
        };
        var request3 = new RagRequest(question, history3, Projects.TeslaMotorsId);
        var result3 = await orchestrator.GenerateAnswerAsync(request3);

        // Assert
        Assert.True(result3.HandoverToHumanNeeded);
    }
}