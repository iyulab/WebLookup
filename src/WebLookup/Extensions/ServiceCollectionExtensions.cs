using Microsoft.Extensions.DependencyInjection;

namespace WebLookup;

/// <summary>
/// Registration of WebLookup in a dependency-injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="WebSearchClient"/> over the providers added in <paramref name="configure"/>, and a singleton <see cref="SiteExplorer"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Adds the search providers.</param>
    /// <returns>The same collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddWebLookup(
        this IServiceCollection services,
        Action<WebLookupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new WebLookupBuilder(services);
        configure(builder);
        builder.Build();
        return services;
    }
}

/// <summary>
/// Collects the search providers that <see cref="ServiceCollectionExtensions.AddWebLookup"/> registers.
/// </summary>
public sealed class WebLookupBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Func<IServiceProvider, ISearchProvider>> _providerFactories = [];

    internal WebLookupBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a Mojeek provider.
    /// </summary>
    /// <param name="apiKey">The Mojeek API key.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is null, empty or white space.</exception>
    public WebLookupBuilder AddMojeek(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _providerFactories.Add(_ => new MojeekSearchProvider(new MojeekSearchOptions
        {
            ApiKey = apiKey
        }));
        return this;
    }

    /// <summary>
    /// Adds a SearchApi provider.
    /// </summary>
    /// <param name="apiKey">The SearchApi API key.</param>
    /// <param name="engine">The SearchApi engine to query, <c>"google"</c> by default.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> or <paramref name="engine"/> is null, empty or white space.</exception>
    public WebLookupBuilder AddSearchApi(string apiKey, string engine = "google")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(engine);
        _providerFactories.Add(_ => new SearchApiProvider(new SearchApiOptions
        {
            ApiKey = apiKey,
            Engine = engine
        }));
        return this;
    }

    /// <summary>
    /// Adds a Tavily provider.
    /// </summary>
    /// <param name="apiKey">The Tavily API key.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is null, empty or white space.</exception>
    public WebLookupBuilder AddTavily(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _providerFactories.Add(_ => new TavilySearchProvider(new TavilySearchOptions
        {
            ApiKey = apiKey
        }));
        return this;
    }

    /// <summary>
    /// Adds a Google Custom Search provider over the engines added in <paramref name="configure"/>.
    /// </summary>
    /// <param name="configure">Adds at least one engine.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No engine was added.</exception>
    public WebLookupBuilder AddGoogle(Action<GoogleSearchBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var google = new GoogleSearchBuilder();
        configure(google);
        var options = google.Build();
        _providerFactories.Add(_ => new GoogleSearchProvider(options));
        return this;
    }

    /// <summary>
    /// Adds a DuckDuckGo provider (no API key) with its default region.
    /// </summary>
    /// <returns>This builder.</returns>
    public WebLookupBuilder AddDuckDuckGo()
    {
        _providerFactories.Add(_ => new DuckDuckGoSearchProvider());
        return this;
    }

    /// <summary>
    /// Adds a DuckDuckGo provider (no API key) for a region.
    /// </summary>
    /// <param name="region">The DuckDuckGo region code, for example <c>"us-en"</c>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="region"/> is null, empty or white space.</exception>
    public WebLookupBuilder AddDuckDuckGo(string region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        _providerFactories.Add(_ => new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions
        {
            Region = region
        }));
        return this;
    }

    internal void Build()
    {
        var factories = _providerFactories.ToArray();

        _services.AddSingleton(sp =>
        {
            var providers = factories.Select(f => f(sp)).ToArray();
            return new WebSearchClient(providers);
        });

        _services.AddSingleton<SiteExplorer>();
    }
}

/// <summary>
/// Collects the Google Custom Search engines of <see cref="WebLookupBuilder.AddGoogle"/>.
/// </summary>
public sealed class GoogleSearchBuilder
{
    private readonly List<GoogleSearchEngine> _engines = [];

    /// <summary>
    /// Adds a Custom Search engine; several engines are searched in parallel and their results merged.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="cx">The search engine id (<c>cx</c>).</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> or <paramref name="cx"/> is null, empty or white space.</exception>
    public GoogleSearchBuilder AddEngine(string apiKey, string cx)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(cx);
        _engines.Add(new GoogleSearchEngine { ApiKey = apiKey, Cx = cx });
        return this;
    }

    internal GoogleSearchOptions Build()
    {
        if (_engines.Count == 0)
            throw new InvalidOperationException("At least one engine must be added.");
        return new GoogleSearchOptions { Engines = _engines.ToArray() };
    }
}
