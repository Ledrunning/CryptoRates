using System.Diagnostics;

namespace CryptoRates.Tests.Services.TestHelpers;

internal static class TestWait
{
    /// <summary>Polls a condition for up to five seconds. For background work that has no event to await.</summary>
    public static async Task UntilAsync(Func<bool> condition, string what)
    {
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 5;
        while (!condition())
        {
            if (Stopwatch.GetTimestamp() > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {what}");
            }

            await Task.Delay(5);
        }
    }
}
