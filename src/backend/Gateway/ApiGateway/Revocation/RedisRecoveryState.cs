namespace ApiGateway.Revocation;

public sealed class RedisRecoveryState
{
    private readonly object stateLock = new();
    private string? runId;
    private DateTimeOffset protectedUntil;
    private bool hadFailure;

    public void Failed()
    {
        lock (stateLock) hadFailure = true;
    }

    public bool Observe(string currentRunId, long uptime, DateTimeOffset now)
    {
        lock (stateLock)
        {
            if (hadFailure || (runId is not null && runId != currentRunId))
                protectedUntil = now.AddSeconds(930);
            if (runId is null && uptime < 930)
                protectedUntil = now.AddSeconds(930 - uptime);
            runId = currentRunId;
            hadFailure = false;
            return now < protectedUntil;
        }
    }
}
