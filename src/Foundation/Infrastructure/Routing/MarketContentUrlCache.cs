using EPiServer.Core;
using EPiServer.Core.Routing.Internal;
using EPiServer.Core.Routing.Pipeline;
using Mediachase.Commerce;
using System;

namespace SonDo.Infrastructure.Routing;

public class MarketContentUrlCache : IContentUrlCache
{
    private readonly IContentUrlCache _contentUrlCache;

    private readonly ICurrentMarket _currentMarket;
    
    public MarketContentUrlCache(
        IContentUrlCache contentUrlCache,
        ICurrentMarket currentMarket
    )
    {
        _contentUrlCache = contentUrlCache;
        _currentMarket = currentMarket;
    }

    public GeneratedUrl Get(ContentUrlCacheContext context)
    {
        var market = _currentMarket.GetCurrentMarket();
        var newContext = new ContentUrlCacheContextWrapper(context.UrlGeneratorContext, context.UrlGeneratorOptions, market.MarketId.ToString());

        return _contentUrlCache.Get(newContext);
    }

    public void Insert(GeneratedUrl url, ContentUrlCacheContext context)
    {
        var market = _currentMarket.GetCurrentMarket();
        var newContext = new ContentUrlCacheContextWrapper(context.UrlGeneratorContext, context.UrlGeneratorOptions, market.MarketId.ToString());
        _contentUrlCache.Insert(url, newContext);
    }

    public void Remove(ContentReference contentLink)
    {
        _contentUrlCache.Remove(contentLink);
    }
}

public class ContentUrlCacheContextWrapper : ContentUrlCacheContext
{
    public string MarketSegment { get; }

    public ContentUrlCacheContextWrapper(
        UrlGeneratorContext urlGeneratorContext,
        UrlGeneratorOptions urlGeneratorOptions, string currentMarketId)
        : base(urlGeneratorContext, urlGeneratorOptions)
    {
        MarketSegment = currentMarketId;
    }

    public override int GetHashCode()
    {
        var hashCodeCombiner = new HashCode();
        hashCodeCombiner.Add(base.GetHashCode());
        hashCodeCombiner.Add(MarketSegment);
        return hashCodeCombiner.ToHashCode();
    }
}