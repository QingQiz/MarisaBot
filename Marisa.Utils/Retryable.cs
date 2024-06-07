namespace Marisa.Utils;

public static class Retryable
{
    public static async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int retry = 0, TimeSpan delay = default)
    {
        var el = new List<Exception>();
        while (retry >= 0)
        {
            try
            {
                return await action();
            }
            catch (Exception e)
            {
                el.Add(e);
            }

            if (delay != default)
            {
                await Task.Delay(delay);
            }
            retry -= 1;
        }

        throw new AggregateException("Max retry reached, see InnerExceptions for details", el);
    }

    public static T WithRetry<T>(Func<T> action, int retry = 0, TimeSpan delay = default)
    {
        return WithRetryAsync(() => Task.Run(action), retry, delay).Result;
    }
}