using StackExchange.Redis;

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

            // TODO: Replace this with real job-processing logic.
            // Example: parse JSON payload and call downstream services.
            await Task.Delay(500, cancellationToken);

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
