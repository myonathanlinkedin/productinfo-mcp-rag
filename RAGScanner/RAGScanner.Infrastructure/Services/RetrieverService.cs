﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

public class RetrieverService : IRetrieverService
{
    private readonly ILogger<RetrieverService> logger;
    private readonly HttpClient httpClient;
    private readonly IEmbeddingService embeddingService;
    private readonly int defaultTopK = 10;
    private readonly int maxTopK = 50;
    private readonly ApplicationSettings applicationSettings;

    public RetrieverService(
        ILogger<RetrieverService> logger,
        HttpClient httpClient,
        IEmbeddingService embeddingService,
        ApplicationSettings applicationSettings)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        this.embeddingService = embeddingService;
        this.applicationSettings = applicationSettings;
    }

    public async Task<Result<List<DocumentVector>>> RetrieveAllDocumentsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving all documents from vector store (full fetch)");

        var endpoint = $"{applicationSettings.Qdrant.Endpoint}/collections/{applicationSettings.Qdrant.CollectionName}/points/scroll";
        int offset = 0;
        int limit = GetSmartLimit();
        var allDocumentVectors = new List<DocumentVector>();

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var payload = new { limit, offset, with_payload = true, with_vector = true };
                var jsonContent = JsonConvert.SerializeObject(payload);
                var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(endpoint, requestContent, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    logger.LogError("Failed to retrieve vectors. StatusCode: {StatusCode}, Response: {ResponseBody}", response.StatusCode, responseBody);
                    return Result<List<DocumentVector>>.Failure(new List<string> { "Failed to retrieve vectors from Qdrant." });
                }

                var responseBodyContent = await response.Content.ReadAsStringAsync();
                var qdrantResponse = JsonConvert.DeserializeObject<QdrantQueryResponse>(responseBodyContent);

                if (qdrantResponse?.Result == null || !qdrantResponse.Result.Any())
                {
                    break;
                }

                // Use Select and ToList to transform and collect results
                var newVectors = qdrantResponse.Result.Select(MapResultToDocumentVector).ToList();
                allDocumentVectors.AddRange(newVectors);
                offset += limit;

                if (allDocumentVectors.Count >= maxTopK * 100)
                {
                    logger.LogInformation("Reached maximum cap for all document retrieval.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during full document retrieval.");
            return Result<List<DocumentVector>>.Failure(new List<string> { "Error during document retrieval." });
        }

        return Result<List<DocumentVector>>.SuccessWith(allDocumentVectors);
    }

    public async Task<Result<List<DocumentVector>>> RetrieveDocumentsByQueryAsync(string queryText, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving documents matching query: {QueryText}", queryText);

        var embeddingVector = await embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken);
        if (embeddingVector == null || embeddingVector.Length == 0)
        {
            logger.LogError("Failed to generate embedding for query text.");
            return Result<List<DocumentVector>>.Failure(new List<string> { "Embedding generation failed." });
        }

        var endpoint = $"{applicationSettings.Qdrant.Endpoint}/collections/{applicationSettings.Qdrant.CollectionName}/points/search";
        int topK = GetSmartTopK();
        var payload = new { vector = embeddingVector, top = topK, with_payload = true, with_vector = true };
        var jsonContent = JsonConvert.SerializeObject(payload);
        var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(endpoint, requestContent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to search vectors. StatusCode: {StatusCode}, Response: {ResponseBody}", response.StatusCode, responseBody);
                return Result<List<DocumentVector>>.Failure(new List<string> { "Failed to search vectors in Qdrant." });
            }

            var responseBodyContent = await response.Content.ReadAsStringAsync();
            var qdrantResponse = JsonConvert.DeserializeObject<QdrantQueryResponse>(responseBodyContent);

            // Use null propagation and Select to handle nulls and transform the result
            var matchingDocuments = qdrantResponse?.Result?.Select(MapResultToDocumentVector).ToList() ?? new List<DocumentVector>();

            return Result<List<DocumentVector>>.SuccessWith(matchingDocuments);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during document search.");
            return Result<List<DocumentVector>>.Failure(new List<string> { "Error during document search." });
        }
    }

    private DocumentVector MapResultToDocumentVector(QdrantResult result) =>
        new DocumentVector
        {
            Metadata = new DocumentMetadata
            {
                Id = new Guid(result.Id),
                Url = result.Payload?.Url,
                SourceType = result.Payload?.SourceType,
                Content = result.Payload?.Content,
                Title = result.Payload?.Title,
                ScrapedAt = result.Payload?.ScrapedAt ?? default(DateTime)
            },
            Embedding = result.Vector.ToArray()
        };

    private int GetSmartTopK()
    {
        return SystemUnderLoad() ? Math.Max(3, defaultTopK / 2) : defaultTopK;
    }

    private int GetSmartLimit()
    {
        return SystemUnderLoad() ? Math.Max(100, defaultTopK * 5) : defaultTopK * 10;
    }

    private bool SystemUnderLoad()
    {
        const double cpuThreshold = 80.0;
        const long memoryThresholdBytes = 80L * 1024 * 1024 * 1024;
        double cpuUsage = GetCpuUsagePercentage();
        long memoryUsage = GetMemoryUsage();
        return cpuUsage > cpuThreshold || memoryUsage > memoryThresholdBytes;
    }

    private double GetCpuUsagePercentage()
    {
        using var cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
        cpuCounter.NextValue();
        Thread.Sleep(500);
        return cpuCounter.NextValue();
    }

    private long GetMemoryUsage()
    {
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        return gcMemoryInfo.TotalCommittedBytes - gcMemoryInfo.TotalAvailableMemoryBytes;
    }
}
