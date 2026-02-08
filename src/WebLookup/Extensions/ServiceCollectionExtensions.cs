using Microsoft.Extensions.DependencyInjection;

namespace WebLookup;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebLookup(
        this IServiceCollection services,
        Action<WebLookupBuilder> configure)
    {
        var builder = new WebLookupBuilder(services);
        configure(builder);
        builder.Build();
        return services;
    }
}

public sealed class WebLookupBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Func<IServiceProvider, ISearchProvider>> _providerFactories = [];

    internal WebLookupBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public WebLookupBuilder AddMojeek(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _providerFactories.Add(_ => new MojeekSearchProvider(new MojeekSearchOptions
        {
            ApiKey = apiKey
        }));
        return this;
    }

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

    public WebLookupBuilder AddTavily(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _providerFactories.Add(_ => new TavilySearchProvider(new TavilySearchOptions
        {
            ApiKey = apiKey
        }));
        return this;
    }

    public WebLookupBuilder AddGoogle(Action<GoogleSearchBuilder> configure)
    {
        var google = new GoogleSearchBuilder();
        configure(google);
        var options = google.Build();
        _providerFactories.Add(_ => new GoogleSearchProvider(options));
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

public sealed class GoogleSearchBuilder
{
    private readonly List<GoogleSearchEngine> _engines = [];

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
