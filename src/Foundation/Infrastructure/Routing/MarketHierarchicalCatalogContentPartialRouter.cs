using EPiServer.Core.Routing;
using EPiServer.Core.Routing.Pipeline;
using EPiServer.Framework;
using EPiServer.Logging;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using EPiServer.Web.Routing.Segments;
using Mediachase.Commerce.Extensions;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using EPiServer.Commerce.Routing;
using Mediachase.Commerce;
using EPiServer.Core;
using EPiServer.Commerce.Catalog.ContentTypes;
using Mediachase.Commerce.Markets;
using System;
using EPiServer;

namespace SonDo.Infrastructure.Routing;

/// <summary>
/// Partial router for catalog content, which handles hierarchical structure
/// </summary>
public class MarketHierarchicalCatalogContentPartialRouter : HierarchicalCatalogPartialRouter,
    ICommerceRouter, IPartialRouter<PageData, CatalogContentBase>, IPartialRouter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContentLanguageAccessor _contentLanguageAccessor;
    private readonly IMarketService _marketService;
    private readonly ICurrentMarket _currentMarket;

    private static readonly ILogger _log = LogManager.GetLogger(typeof(MarketHierarchicalCatalogContentPartialRouter));

    /// <summary>
    /// Initialize a new instance of <see cref="T:EPiServer.Commerce.Routing.HierarchicalCatalogPartialRouter" />
    /// </summary>
    /// <param name="routeStartingPoint">A delegate that will return the content the route should be based on.</param>
    /// <param name="commerceRoot">The root node where this route will look for commerce content when matching route segments.</param>
    /// <param name="enableOutgoingSeoUri">Enables seo uri for outgoing route.</param>
    public MarketHierarchicalCatalogContentPartialRouter(
        Func<ContentReference> routeStartingPoint,
        CatalogContentBase commerceRoot,
        bool enableOutgoingSeoUri)
        : this(routeStartingPoint, commerceRoot, enableOutgoingSeoUri,
            ServiceLocator.Current.GetInstance<IContentLoader>(),
            ServiceLocator.Current.GetInstance<IRoutingSegmentLoader>(),
            ServiceLocator.Current.GetInstance<IContentVersionRepository>(),
            ServiceLocator.Current.GetInstance<IUrlSegmentRouter>(),
            ServiceLocator.Current.GetInstance<IContentLanguageSettingsHandler>(),
            ServiceLocator.Current.GetInstance<IHttpContextAccessor>(),
            ServiceLocator.Current.GetInstance<RoutingOptions>(),
            ServiceLocator.Current.GetInstance<IContentLanguageAccessor>(),
            ServiceLocator.Current.GetInstance<SeoUriContentReferenceResolver>(),
            ServiceLocator.Current.GetInstance<IMarketService>(),
            ServiceLocator.Current.GetInstance<ICurrentMarket>())
    {
    }

    /// <summary>
    /// Initialize a new instance of <see cref="T:EPiServer.Commerce.Routing.HierarchicalCatalogPartialRouter" />
    /// </summary>
    /// <param name="routeStartingPoint">A delegate that will return the content the route should be based on.</param>
    /// <param name="commerceRoot">The root node where this route will look for commerce content when matching route segments.</param>
    /// <param name="supportSeoUri">Enables seo uri for the route.</param>
    /// <param name="contentLoader">The content loader service.</param>
    /// <param name="routingSegmentLoader">The routing segment loader service.</param>
    /// <param name="contentVersionRepository">The content version repository.</param>
    /// <param name="urlSegmentRouter">The url segment router.</param>
    /// <param name="contentLanguageSettingsHandler">The content language settings handler</param>
    /// <param name="httpContextAccessor">The http context accessor</param>
    /// <param name="routingOptions">Routing options</param>
    /// <param name="contentLanguageAccessor">The content language.</param>
    /// <param name="uriContentResolver">The uri to content link resolver.</param>
    /// <param name="marketService">The market service.</param>
    /// <param name="currentMarket">The current market.</param>
    public MarketHierarchicalCatalogContentPartialRouter(
        Func<ContentReference> routeStartingPoint,
        CatalogContentBase commerceRoot,
        bool supportSeoUri,
        IContentLoader contentLoader,
        IRoutingSegmentLoader routingSegmentLoader,
        IContentVersionRepository contentVersionRepository,
        IUrlSegmentRouter urlSegmentRouter,
        IContentLanguageSettingsHandler contentLanguageSettingsHandler,
        IHttpContextAccessor httpContextAccessor,
        RoutingOptions routingOptions,
        IContentLanguageAccessor contentLanguageAccessor,
        SeoUriContentReferenceResolver uriContentResolver,
        IMarketService marketService,
        ICurrentMarket currentMarket)
        : base(routeStartingPoint, commerceRoot, supportSeoUri, contentLoader, routingSegmentLoader, contentVersionRepository,
            urlSegmentRouter, contentLanguageSettingsHandler, httpContextAccessor, routingOptions, contentLanguageAccessor,
            uriContentResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _contentLanguageAccessor = contentLanguageAccessor;
        _currentMarket = currentMarket;
        _marketService = marketService;
    }

    /// <summary>
    /// Matches a route by traversing the route segments and matching them with the url segments of the commerce content.
    /// </summary>
    /// <remarks>
    /// Only <see cref="T:EPiServer.Commerce.Catalog.ContentTypes.CatalogContentBase" /> will be returned. If the route matches any other type of content, that will be ignored.
    /// </remarks>
    /// <example>http://mysite/catalog/catalognode/entry</example>
    /// <param name="content">The content that the page route has been able to route to.</param>
    /// <param name="urlResolverContext">The segment context containing the remaining part of url.</param>
    /// <returns>A <see cref="T:EPiServer.Core.ContentReference" /> to the mathced data or null if the remaining part did not match.</returns>
    public override object RoutePartial(PageData content, UrlResolverContext urlResolverContext)
    {
        if (_log.IsDebugEnabled())
            _log.DebugBeginMethod(nameof(RoutePartial), content, urlResolverContext);
        Validator.ThrowIfNull("segmentContext", urlResolverContext);

        if (!content.ContentLink.CompareToIgnoreWorkID(RouteStartingPoint))
            return null;

        var remainingSegment = urlResolverContext.GetNextRemainingSegment(urlResolverContext.RemainingPath);
        if (string.IsNullOrEmpty(remainingSegment.Next))
            return null;

        if (MarketRoutingHelper.ProcessMarketSegment(_currentMarket, _marketService, urlResolverContext, remainingSegment))
        {
            remainingSegment = urlResolverContext.GetNextRemainingSegment(urlResolverContext.RemainingPath);
        }

        var cultureInfo = urlResolverContext.RequestedLanguage ?? _contentLanguageAccessor.Language;

        var catalogContentBaseTemp = GetCatalogContentByUri(remainingSegment, urlResolverContext, cultureInfo);
        
        var catalogContentBase = EnableOutgoingSeoUri ? catalogContentBaseTemp
            : catalogContentBaseTemp is ProductContent ? catalogContentBaseTemp
            : GetCatalogContentRecursive(CommerceRoot, remainingSegment, urlResolverContext, cultureInfo);
        
        if (catalogContentBase != null)
            urlResolverContext.Content = catalogContentBase;

        return catalogContentBase;
    }

    public PartialRouteData GetPartialVirtualPath(CatalogContentBase content, UrlGeneratorContext urlGeneratorContext)
    {
        if (_log.IsDebugEnabled())
            _log.DebugBeginMethod(nameof(GetPartialVirtualPath), content, urlGeneratorContext);
        if (!IsValidRoutedContent(content))
            return null;

        if (EnableOutgoingSeoUri && urlGeneratorContext.ContextMode == ContextMode.Default)
        {
            string str = null;
            if (content is ISearchEngineInformation engineInformation)
                str = engineInformation.SeoUri;

            if (!string.IsNullOrEmpty(str))
            {
                urlGeneratorContext.RouteValues.Remove(RoutingConstants.ContentLinkKey);
                return new PartialRouteData()
                {
                    BasePathRoot = ContentReference.IsNullOrEmpty(ContentReference.StartPage)
                        ? ContentReference.RootPage
                        : ContentReference.StartPage,
                    PartialVirtualPath = str
                };
            }
        }

        var currentMarket = _currentMarket.GetCurrentMarket();
        var marketId = currentMarket.MarketId.ToString().ToLower();

        CultureInfo cultureInfo = urlGeneratorContext.Language ?? _contentLanguageAccessor.Language;
        string virtualPath;

        if (content is ProductContent productContent)
        {
            virtualPath = BuildVirtualPathForProduct(productContent);
        }
        else
        {
            if (!TryGetVirtualPath(_httpContextAccessor.HttpContext, content, cultureInfo?.Name, out virtualPath))
                return null;
        }

        var editOrPreviewMode = SegmentHelper.GetModifiedVirtualPathInEditOrPreviewMode
            (content.ContentLink, virtualPath, urlGeneratorContext.ContextMode);

        return new PartialRouteData()
        {
            BasePathRoot = RouteStartingPoint,
            PartialVirtualPath = $"{marketId}/{editOrPreviewMode}"
        };
    }

    private string BuildVirtualPathForProduct(ProductContent content)
    {
        return content.SeoUri;
    }

}