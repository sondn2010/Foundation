using EPiServer;
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Core;
using EPiServer.Core.Routing;
using EPiServer.Logging;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using Mediachase.Commerce.Extensions;
using System;
using System.ComponentModel;

namespace SonDo.Infrastructure.Routing;

public class MarketRouteHelper
{
    private static readonly ILogger _log = LogManager.GetLogger(typeof(MarketRouteHelper));

    public static void MapSchumacherRouter()
    {
        // The default routing from Foundation
        //CatalogRouteHelper.MapDefaultHierarchialRouter(false);
        
        // Config market routing for CMS content.
        RegisterPartialRouter(new MarketHierarchicalPageDataPartialRouting());
        
        // Config market routing for Commerce content
        var referenceConverter = ServiceLocator.Current.GetInstance<ReferenceConverter>();
        var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();

        var commerceRoot = contentLoader.GetChildren<CatalogContent>(referenceConverter.GetRootLink())
            .FirstOrDefault(x => string.Equals(x.Name, Catalog.DefaultCatalogueName, StringComparison.OrdinalIgnoreCase));
        if (commerceRoot == null)
            throw new Exception($"The {Catalog.DefaultCatalogueName} category of type Schumacher Category need to be created first.");

        MapDefaultHierarchialRouter(
            () =>
                !ContentReference.IsNullOrEmpty(SiteDefinition.Current.StartPage)
                    ? SiteDefinition.Current.StartPage
                    : SiteDefinition.Current.RootPage,
            commerceRoot,
            false);
    }
    
    public static void MapDefaultHierarchialRouter()
    {
        MapDefaultHierarchialRouter(false);
    }

    /// <summary>
    /// Registers a default partial router for catalog content.
    /// </summary>
    /// <param name="enableOutgoingSeoUri">if set to <c>true</c> the outgoing links for catalog items will use the SEO URL.</param>
    public static void MapDefaultHierarchialRouter(bool enableOutgoingSeoUri)
    {
        if (_log.IsDebugEnabled())
            _log.DebugBeginMethod(nameof(MapDefaultHierarchialRouter), (object)enableOutgoingSeoUri);

        MapDefaultHierarchialRouter(() =>
            !ContentReference.IsNullOrEmpty(SiteDefinition.Current.StartPage) ? SiteDefinition.Current.StartPage : SiteDefinition.Current.RootPage, enableOutgoingSeoUri);
    }

    public static void MapDefaultHierarchialRouter(
        Func<ContentReference> startingPoint,
        bool enableOutgoingSeoUri)
    {
        var referenceConverter = ServiceLocator.Current.GetInstance<ReferenceConverter>();
        var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();

        CatalogContentBase commerceRoot = contentLoader.Get<CatalogContentBase>(referenceConverter.GetRootLink());
        MapDefaultHierarchialRouter(startingPoint, commerceRoot, enableOutgoingSeoUri);
    }

    /// <summary>
    /// Registers a default partial router for catalog content.
    /// </summary>
    /// <param name="startingPoint">The starting point where the partial route will start.</param>
    /// <param name="commerceRoot"></param>
    /// <param name="enableOutgoingSeoUri">if set to <c>true</c> the outgoing links for catalog items will use the SEO URL.</param>
    public static void MapDefaultHierarchialRouter(Func<ContentReference> startingPoint, CatalogContentBase commerceRoot, bool enableOutgoingSeoUri)
    {
        if (_log.IsDebugEnabled())
            _log.DebugBeginMethod(nameof(MapDefaultHierarchialRouter), startingPoint, enableOutgoingSeoUri);

        RegisterPartialRouter(new MarketHierarchicalCatalogContentPartialRouter(startingPoint, commerceRoot, enableOutgoingSeoUri));
    }

    private static void RegisterPartialRouter<TContent, TRoutedData>(IPartialRouter<TContent, TRoutedData> partialRouter)
        where TContent : class, IContent
        where TRoutedData : class
    {
        ServiceLocator.Current.GetInstance<PartialRouteHandler>()
            .RegisterPartialRouter(new PartialRouter<TContent, TRoutedData>(partialRouter));
    }
}
