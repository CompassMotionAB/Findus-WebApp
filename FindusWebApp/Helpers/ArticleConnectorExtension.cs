using Fortnox.SDK.Interfaces;
using Fortnox.SDK.Search;
using Fortnox.SDK.Entities;
using Fortnox.SDK.Connectors;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace FindusWebApp.Helpers
{
    public static class ArticleConnectorExtension
    {

        private static readonly MemoryCacheEntryOptions _memoryCacheOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromMinutes(30));
        public static async Task<bool> HasArticleAsync(
            this IArticleConnector connector,
            string sku,
            IMemoryCache memoryCache
        )
        {
            if (!memoryCache.TryGetValue(sku, out bool hasArticle))
            {
                var productCollection = await connector.FindAsync(
                    new ArticleSearch { ArticleNumber = sku }
                );
                var product = productCollection.Entities.FirstOrDefault();
                hasArticle = product != null;

                memoryCache.Set(sku, hasArticle, _memoryCacheOptions);
            }
            return hasArticle;
        }
    }
}
