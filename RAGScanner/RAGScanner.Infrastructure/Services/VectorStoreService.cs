using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

public class VectorStoreService : IVectorStoreService
{
    private readonly ILogger<VectorStoreService> logger;
    private readonly QdrantClient qdrantClient;
    private readonly ApplicationSettings.QdrantSettings qdrantSettings;
    private readonly float similarityThreshold;

    public VectorStoreService(
        ILogger<VectorStoreService> logger,
        ApplicationSettings appSettings)
    {
        this.logger = logger;
        qdrantSettings = appSettings.Qdrant;
        similarityThreshold = appSettings.Qdrant.SimilarityThreshold;

        // Initialize the Qdrant client
        var uri = new Uri(qdrantSettings.Endpoint);

        // Set up the client configuration depending on whether HTTPS is used
        if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            // Check if API key is provided
            if (!string.IsNullOrEmpty(qdrantSettings.ApiKey))
            {
                qdrantClient = new QdrantClient(
                    host: uri.Host,
                    port: uri.Port != -1 ? uri.Port : 6334,
                    https: true,
                    apiKey: qdrantSettings.ApiKey);
            }
            else
            {
                qdrantClient = new QdrantClient(
                    host: uri.Host,
                    port: uri.Port != -1 ? uri.Port : 6334,
                    https: true);
            }
        }
        else
        {
            // Check if API key is provided
            if (!string.IsNullOrEmpty(qdrantSettings.ApiKey))
            {
                qdrantClient = new QdrantClient(
                    host: uri.Host,
                    port: uri.Port != -1 ? uri.Port : 6333,
                    apiKey: qdrantSettings.ApiKey);
            }
            else
            {
                qdrantClient = new QdrantClient(
                    host: uri.Host,
                    port: uri.Port != -1 ? uri.Port : 6333);
            }
        }
    }

    // Ensure collection exists
    private async Task EnsureCollectionExistsAsync(int vectorSize)
    {
        try
        {
            var collections = await qdrantClient.ListCollectionsAsync();
            bool collectionExists = collections.Any(c => c == qdrantSettings.CollectionName);

            if (!collectionExists)
            {
                logger.LogWarning("Collection '{CollectionName}' not found. Attempting to create it.", qdrantSettings.CollectionName);
                await CreateCollectionAsync(vectorSize);
            }
            else
            {
                logger.LogInformation("Collection '{CollectionName}' exists.", qdrantSettings.CollectionName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking collection existence");
            throw;
        }
    }

    // Create collection if doesn't exist
    private async Task CreateCollectionAsync(int vectorSize)
    {
        try
        {
            // Check if collection exists first to avoid unnecessary create attempt
            var collections = await qdrantClient.ListCollectionsAsync();
            bool collectionExists = collections.Any(c => c == qdrantSettings.CollectionName);

            if (collectionExists)
            {
                logger.LogInformation("Collection '{CollectionName}' already exists in Qdrant. Skipping creation.", qdrantSettings.CollectionName);
                return;
            }

            // Create the collection
            await qdrantClient.CreateCollectionAsync(
                collectionName: qdrantSettings.CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)vectorSize,
                    Distance = Distance.Cosine
                });

            logger.LogInformation("Successfully created collection '{CollectionName}' in Qdrant.", qdrantSettings.CollectionName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during collection creation in Qdrant.");
            // Continue processing, do not throw an exception to allow insertion to proceed
        }
    }

    // Save document vector after checking for similar vectors
    public async Task SaveDocumentAsync(DocumentVector documentVector, int vectorSize)
    {
        await EnsureCollectionExistsAsync(vectorSize);

        try
        {
            // Step 1: Search for similar vectors
            if (await IsVectorSimilarAsync(documentVector.Embedding))
            {
                logger.LogInformation("Vector is too similar to an existing one. Skipping save.");
                return; // Skip if vector is too similar
            }

            // Step 2: Insert the vector into Qdrant
            await InsertVectorAsync(documentVector);
            logger.LogInformation("Successfully saved vector for URL: {Url}", documentVector.Metadata.Url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving document vector.");
            throw;
        }
    }

    private async Task<bool> IsVectorSimilarAsync(float[] queryVector)
    {
        try
        {
            var searchResult = await qdrantClient.SearchAsync(
                collectionName: qdrantSettings.CollectionName,
                vector: queryVector,
                limit: 5  // Get the top 5 similar vectors
            );

            // Compare cosine similarity to the top result
            if (searchResult.Any())
            {
                var mostSimilarVector = searchResult.First();
                var similarityScore = mostSimilarVector.Score;

                // Return true if the similarity score is greater than or equal to the threshold
                return similarityScore >= similarityThreshold;
            }

            return false; // No similar vectors found, so it's safe to insert
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching for similar vectors in Qdrant.");
            throw;
        }
    }

    // Insert vector into Qdrant
    private async Task InsertVectorAsync(DocumentVector documentVector)
    {
        try
        {
            var pointId = new PointId { Uuid = Guid.NewGuid().ToString() };

            var payload = new Dictionary<string, Value>
            {
                ["url"] = new Value { StringValue = documentVector.Metadata.Url },
                ["sourceType"] = new Value { StringValue = documentVector.Metadata.SourceType },
                ["title"] = new Value { StringValue = documentVector.Metadata.Title },
                ["content"] = new Value { StringValue = documentVector.Metadata.Content },
                ["scrapedAt"] = new Value { StringValue = documentVector.Metadata.ScrapedAt.ToString() }
            };

            var point = new PointStruct
            {
                Id = pointId,
                Vectors = new Vectors { Vector = new Vector { Data = { documentVector.Embedding } } },
                Payload = { payload }
            };

            await qdrantClient.UpsertAsync(
                collectionName: qdrantSettings.CollectionName,
                points: new List<PointStruct> { point }
            );

            logger.LogInformation("Successfully saved vector for URL: {Url}", documentVector.Metadata.Url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save vector to Qdrant: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}