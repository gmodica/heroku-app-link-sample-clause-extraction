using System.Text.Json;
using ClausesExtractor;
using ClausesExtractor.Models;
using Heroku.Applink.Data;
using Heroku.Applink.Models;
using StackExchange.Redis;
using static Heroku.Applink.Bulk.BulkApi;

namespace worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        if(redisUrl == null) throw new InvalidOperationException("REDIS_URL environment variable is not set");

        var uri = new Uri(redisUrl);
        var userInfoParts = uri.UserInfo.Split(':');
        if (userInfoParts.Length != 2) throw new InvalidOperationException("REDIS_URL is not in the expected format ('redis://user:password@host:port')");

        var configurationOptions = new ConfigurationOptions
        {
            EndPoints = { { uri.Host, uri.Port } },
            Password = userInfoParts[1],
            Ssl = true,
        };
        configurationOptions.CertificateValidation += (sender, cert, chain, errors) => true;
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(configurationOptions);

        var subscriber = redis.GetSubscriber();

        // Subscribe to the "jobs" channel and process incoming messages.
        await subscriber.SubscribeAsync(RedisChannel.Literal("jobs"), (channel, message) =>
        {
            // Fire-and-forget the processing so Redis thread isn't blocked.
            _ = Task.Run(() => ProcessJobAsync(message, stoppingToken));
        });

        _logger.LogInformation("Subscribed to Redis channel 'jobs' and awaiting messages...");

        try
        {
            // Wait until cancellation is requested.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        finally
        {
            try
            {
                await subscriber.UnsubscribeAsync(RedisChannel.Literal("jobs"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unsubscribe from Redis channel 'jobs' during shutdown");
            }

            try
            {
                redis.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose Redis connection");
            }
        }
    }

    private async Task ProcessJobAsync(RedisValue message, CancellationToken cancellationToken)
    {
        try
        {
            var text = (string?)message;
            _logger.LogInformation("Processing job message: {Message}", text);

            ExtractJob? job = System.Text.Json.JsonSerializer.Deserialize<ExtractJob>(text!);
            if (job == null)
            {
                _logger.LogError("Failed to deserialize job message: {Message}", text);
                return;
            }

            try
            {
                var extractor = new Extractor();
                var results = await extractor.ExtractClauses(job.Url);

                Org org = new Org(
                    job.SalesforceContext.AccessToken,
                    job.SalesforceContext.ApiVersion,
                    job.SalesforceContext.Namespace,
                    job.SalesforceContext.OrgId,
                    job.SalesforceContext.DomainUrl,
                    job.SalesforceContext.UserId,
                    job.SalesforceContext.Username,
                    job.SalesforceContext.OrgType
                );

                var dataTableBuilder = org.BulkApi.CreateDataTableBuilder("Name", "Subject__c", "Id__c", "Number__c", "Body__c");
                foreach (var file in results.Files)
                {
                    dataTableBuilder.AddRow(new[] { file.Id ?? "", file.Name ?? "", file.Id ?? "", file.Number ?? "", file.Body ?? "" });
                }
                var dataTable = dataTableBuilder.Build();

                var ingestJobs = await org.BulkApi.IngestAsync("Clause__c", dataTable, "upsert", "Id__c", cancellationToken);
                IngestJobReference bulkJobReference = (ingestJobs.FirstOrDefault() as IngestJobReference)!;

                string bulkJobId = bulkJobReference.Id;
                _logger.LogInformation("Ingested {FileCount} clauses for job {JobId}", results.Files.Count, bulkJobId);

                // wait on completion status
                int totalProcessed = 0;
                int totalFailed = 0;
                while (true) {
                    JsonDocument statusPayload = await org.BulkApi.GetInfoAsync(bulkJobReference, cancellationToken);
                    string status = statusPayload.RootElement.GetProperty("state").GetString() ?? "Unknown";
                    if (status == "JobComplete" || status == "Aborted" || status == "Failed")
                    {
                        totalProcessed = statusPayload.RootElement.GetProperty("numberRecordsProcessed").GetInt32();
                        totalFailed = statusPayload.RootElement.GetProperty("numberRecordsFailed").GetInt32();
                        break;
                    }

                    await Task.Delay(1000, cancellationToken);
                }


                    RecordForCreate eventRecord = new RecordForCreate()
                    {
                        Type = "Clauses_Extraction_Event__e",
                        Fields = new Dictionary<string, object?>
                        {
                            { "User_Id__c", job.SalesforceContext.UserId },
                            { "Job_Id__c", job.JobId },
                            { "Bulk_Job_Id__c", bulkJobId },
                            { "Total_Clauses_Error__c", (results.Errors?.Count ?? 0) + totalFailed },
                            { "Total_Clauses_Submitted__c", totalProcessed }
                        }
                    };
                    await org.DataApi.CreateAsync(eventRecord, cancellationToken);
                }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error extracting clauses for job {JobId} from URL {Url}", job.JobId, job.Url);
                return;
            }

            _logger.LogInformation("Finished processing job message");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job message");
        }
    }
}
