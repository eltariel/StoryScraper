using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace threadmarks_thing
{
    public class RateLimitHandler : DelegatingHandler
    {
        private const int MaxRetries = 5;
        private const int InitialDelay = 200;

        public RateLimitHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        public bool HitRateLimit {get; private set; } = false;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var delay = TimeSpan.FromMilliseconds(InitialDelay);
            HttpResponseMessage response = null;
            for (int i = 0; i < MaxRetries; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    HitRateLimit = false;
                    return response;
                }

                Console.WriteLine($"Rate limited, waiting {delay.TotalSeconds}s");
                await Task.Delay(delay);
                delay *= 2;
                HitRateLimit = true;
            }

            return response;
        }
    }
}
