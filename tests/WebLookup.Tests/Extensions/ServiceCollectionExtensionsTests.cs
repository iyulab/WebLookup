using Microsoft.Extensions.DependencyInjection;

namespace WebLookup.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWebLookup_RegistersWebSearchClient()
    {
        var services = new ServiceCollection();

        services.AddWebLookup(builder =>
        {
            builder.AddTavily("test-key");
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WebSearchClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddWebLookup_RegistersSiteExplorer()
    {
        var services = new ServiceCollection();

        services.AddWebLookup(builder =>
        {
            builder.AddMojeek("test-key");
        });

        using var provider = services.BuildServiceProvider();
        var explorer = provider.GetRequiredService<SiteExplorer>();

        Assert.NotNull(explorer);
    }

    [Fact]
    public void AddMojeek_EmptyApiKey_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder => builder.AddMojeek(""));
        });
    }

    [Fact]
    public void AddSearchApi_EmptyApiKey_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder => builder.AddSearchApi(""));
        });
    }

    [Fact]
    public void AddTavily_EmptyApiKey_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder => builder.AddTavily(""));
        });
    }

    [Fact]
    public void AddSearchApi_EmptyEngine_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder => builder.AddSearchApi("key", engine: ""));
        });
    }

    [Fact]
    public void AddWebLookup_MultipleProviders_AllRegistered()
    {
        var services = new ServiceCollection();

        services.AddWebLookup(builder =>
        {
            builder
                .AddMojeek("mojeek-key")
                .AddSearchApi("searchapi-key")
                .AddTavily("tavily-key");
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WebSearchClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddGoogle_WithEngines_RegistersProvider()
    {
        var services = new ServiceCollection();

        services.AddWebLookup(builder =>
        {
            builder.AddGoogle(google =>
            {
                google.AddEngine("api-key-1", "cx1");
                google.AddEngine("api-key-2", "cx2");
            });
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WebSearchClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddGoogle_EmptyApiKey_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder =>
            {
                builder.AddGoogle(google => google.AddEngine("", "cx1"));
            });
        });
    }

    [Fact]
    public void AddGoogle_EmptyCx_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder =>
            {
                builder.AddGoogle(google => google.AddEngine("key", ""));
            });
        });
    }

    [Fact]
    public void AddGoogle_NoEngines_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddWebLookup(builder =>
            {
                builder.AddGoogle(_ => { });
            });
        });
    }

    [Fact]
    public void AddDuckDuckGo_ZeroConfig_RegistersProvider()
    {
        var services = new ServiceCollection();

        services.AddWebLookup(builder =>
        {
            builder.AddDuckDuckGo();
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WebSearchClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddDuckDuckGo_WithRegion_RegistersProvider()
    {
        var services = new ServiceCollection();

        services.AddWebLookup(builder =>
        {
            builder.AddDuckDuckGo("kr-ko");
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WebSearchClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddDuckDuckGo_EmptyRegion_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddWebLookup(builder => builder.AddDuckDuckGo(""));
        });
    }
}
