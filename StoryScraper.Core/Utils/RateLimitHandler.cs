using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StoryScraper.Core.Utils
{
    public class RateLimitHandler : DelegatingHandler
    {
        private const int MaxRetries = 15;
        private const int BaseDelay = 100;    // first delay will be BaseDelay * 2

        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        public RateLimitHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

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
                    log.Trace($"Not rate limited: URL {response.RequestMessage.RequestUri}");
                    return response;
                }

                log.Trace($"Rate limited, waiting {delay.TotalSeconds}s");
                await Task.Delay(delay, cancellationToken);
            }

            log.Warn($"Rate limited: URL {response?.RequestMessage.RequestUri}");
            return response;
        }
    }
}