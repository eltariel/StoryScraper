using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StoryScraper
{
    public class RateLimitHandler : DelegatingHandler
    {
        private const int MaxRetries = 15;
        private const int BaseDelay = 100;    // first delay will be BaseDelay * 2

        public RateLimitHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        public bool HitRateLimit { get; private set; } = false;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            foreach (var delay in Enumerable
                .Range(0, MaxRetries)
                .Select(i => TimeSpan.FromMilliseconds(BaseDelay * (2 << i))))
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    HitRateLimit = false;
                    return response;
                }

                Console.WriteLine($"Rate limited, waiting {delay.TotalSeconds}s");
                await Task.Delay(delay, cancellationToken);
                HitRateLimit = true;
            }

            return response;
        }
    }
}