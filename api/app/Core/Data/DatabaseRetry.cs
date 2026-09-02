using Microsoft.Extensions.Logging;

namespace SeniorCare.Api.App.Core.Data;

public static class DatabaseRetry
{
    public static async Task ExecuteAsync(
        Func<Task> operation,
        ILogger logger,
        CancellationToken cancellationToken = default,
        int maxAttempts = 20)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
                logger.LogWarning(
                    exception,
                    "La base de datos aún no está disponible. Intento {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxAttempts);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}

