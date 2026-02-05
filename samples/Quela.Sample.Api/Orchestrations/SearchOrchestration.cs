using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Effects;
using Quela.Reactive.DSL;

namespace Quela.Sample.Api.Orchestrations;

/// <summary>
/// Search with autocomplete and faceted filtering.
/// Demonstrates debouncing, parallel requests, and fan-out/fan-in.
/// </summary>
public static class SearchOrchestration
{
    public static ReactiveGraph Build()
    {
        return OrchestrationBuilder.Create("search", "Product Search", "1.0.0")
            // Search query field with debouncing
            .Field<string>("query")
                .Default("")
                .BindTo("#search-input")
                .Debounce(300)
                .Add()

            // Filter fields
            .Field<string>("category")
                .Default("")
                .BindTo("#category-filter")
                .Add()

            .Field<decimal>("minPrice")
                .Default(0m)
                .BindTo("#min-price")
                .Add()

            .Field<decimal>("maxPrice")
                .Default(1000m)
                .BindTo("#max-price")
                .Add()

            .Field<string>("sortBy")
                .Default("relevance")
                .BindTo("#sort-by")
                .Add()

            .Field<int>("page")
                .Default(1)
                .BindTo("#current-page")
                .Add()

            .Field<int>("pageSize")
                .Default(20)
                .Add()

            // Autocomplete suggestions (fast, debounced)
            .Effect<AutocompleteResult>("autocomplete")
                .DependsOn("query")
                .WithTimeout(2)
                .WithRetry(1)
                .InCancellationScope("autocomplete")
                .ExecuteAsync(async ctx =>
                {
                    var query = ctx.GetOrDefault<string>(new("query"), "");
                    if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                        return new AutocompleteResult();

                    // Simulate fast autocomplete API
                    await Task.Delay(100, ctx.CancellationToken);

                    var suggestions = new[] { "laptop", "laptop bag", "laptop stand", "laptop cooling pad" }
                        .Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .Take(5)
                        .ToList();

                    return new AutocompleteResult { Suggestions = suggestions };
                })
                .Add()

            // Search trigger (also triggers on filter changes)
            .Trigger("search")
                .OnChange()
                .BindTo("#search-input, #category-filter, #min-price, #max-price, #sort-by")
                .Debounce(TimeSpan.FromMilliseconds(500))
                .Add()

            // Fan-out: search products, get facets, and get recommendations in parallel
            .Effect<SearchResults>("searchProducts")
                .DependsOn("search", "query", "category", "minPrice", "maxPrice", "sortBy", "page", "pageSize")
                .WithTimeout(10)
                .WithRetry(2)
                .InCancellationScope("search")
                .ExecuteAsync(async ctx =>
                {
                    var query = ctx.GetOrDefault<string>(new("query"), "");
                    var category = ctx.GetOrDefault<string>(new("category"), "");
                    var minPrice = ctx.GetOrDefault<decimal>(new("minPrice"), 0m);
                    var maxPrice = ctx.GetOrDefault<decimal>(new("maxPrice"), 1000m);
                    var sortBy = ctx.GetOrDefault<string>(new("sortBy"), "relevance");
                    var page = ctx.GetOrDefault<int>(new("page"), 1);
                    var pageSize = ctx.GetOrDefault<int>(new("pageSize"), 20);

                    ctx.Logger.Info("Searching for: {Query}", query);

                    // Simulate search API
                    await Task.Delay(300, ctx.CancellationToken);

                    return new SearchResults
                    {
                        Query = query,
                        TotalResults = 150,
                        Page = page,
                        PageSize = pageSize,
                        Products = Enumerable.Range(1, pageSize)
                            .Select(i => new Product
                            {
                                Id = $"prod-{page}-{i}",
                                Name = $"{query} Product {i}",
                                Price = 10m + (i * 5.5m),
                                Category = category
                            })
                            .ToList()
                    };
                })
                .Add()

            .Effect<FacetResults>("getFacets")
                .DependsOn("search", "query")
                .WithTimeout(5)
                .WithRetry(1)
                .ExecuteAsync(async ctx =>
                {
                    var query = ctx.GetOrDefault<string>(new("query"), "");

                    await Task.Delay(200, ctx.CancellationToken);

                    return new FacetResults
                    {
                        Categories = new List<FacetValue>
                        {
                            new("Electronics", 45),
                            new("Accessories", 32),
                            new("Office", 28)
                        },
                        PriceRanges = new List<FacetValue>
                        {
                            new("$0-$50", 20),
                            new("$50-$100", 35),
                            new("$100+", 50)
                        },
                        Brands = new List<FacetValue>
                        {
                            new("Brand A", 15),
                            new("Brand B", 25),
                            new("Brand C", 30)
                        }
                    };
                })
                .Add()

            .Effect<RecommendationResult>("getRecommendations")
                .DependsOn("search", "query", "category")
                .WithTimeout(3)
                .WithRetry(1)
                .ExecuteAsync(async ctx =>
                {
                    var query = ctx.GetOrDefault<string>(new("query"), "");

                    await Task.Delay(150, ctx.CancellationToken);

                    return new RecommendationResult
                    {
                        RelatedSearches = new[] { $"{query} deals", $"{query} reviews", $"best {query}" },
                        TrendingProducts = new[]
                        {
                            new Product { Id = "trend-1", Name = "Trending Item 1", Price = 99.99m },
                            new Product { Id = "trend-2", Name = "Trending Item 2", Price = 149.99m }
                        }
                    };
                })
                .Add()

            // Fan-in: aggregate all search results
            .Aggregate<object, AggregatedSearchResult>("aggregatedResults")
                .FromSources("searchProducts", "getFacets", "getRecommendations")
                .WaitAll()
                .WithTimeout(TimeSpan.FromSeconds(15))
                .Aggregate(results =>
                {
                    var products = results.OfType<SearchResults>().FirstOrDefault();
                    var facets = results.OfType<FacetResults>().FirstOrDefault();
                    var recommendations = results.OfType<RecommendationResult>().FirstOrDefault();

                    return new AggregatedSearchResult
                    {
                        Products = products ?? new SearchResults(),
                        Facets = facets ?? new FacetResults(),
                        Recommendations = recommendations ?? new RecommendationResult()
                    };
                })
                .Add()

            // Pagination triggers
            .Trigger("nextPage")
                .OnClick()
                .BindTo("#next-page-btn")
                .Add()

            .Trigger("prevPage")
                .OnClick()
                .BindTo("#prev-page-btn")
                .Add()

            // Computed: pagination state
            .Computed<bool>("hasNextPage")
                .DependsOn("aggregatedResults", "page", "pageSize")
                .Compute(ctx =>
                {
                    var results = ctx.GetOrDefault<AggregatedSearchResult>(new("aggregatedResults"), new());
                    var page = ctx.GetOrDefault<int>(new("page"), 1);
                    var pageSize = ctx.GetOrDefault<int>(new("pageSize"), 20);

                    return page * pageSize < results.Products.TotalResults;
                })
                .BindTo("#next-page-btn")
                .Add()

            .Computed<bool>("hasPrevPage")
                .DependsOn("page")
                .Compute(ctx => ctx.GetOrDefault<int>(new("page"), 1) > 1)
                .BindTo("#prev-page-btn")
                .Add()

            .Build();
    }

    // DTOs
    public record AutocompleteResult
    {
        public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
    }

    public record Product
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public decimal Price { get; init; }
        public string Category { get; init; } = "";
    }

    public record SearchResults
    {
        public string Query { get; init; } = "";
        public int TotalResults { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public IReadOnlyList<Product> Products { get; init; } = Array.Empty<Product>();
    }

    public record FacetValue(string Name, int Count);

    public record FacetResults
    {
        public IReadOnlyList<FacetValue> Categories { get; init; } = Array.Empty<FacetValue>();
        public IReadOnlyList<FacetValue> PriceRanges { get; init; } = Array.Empty<FacetValue>();
        public IReadOnlyList<FacetValue> Brands { get; init; } = Array.Empty<FacetValue>();
    }

    public record RecommendationResult
    {
        public IReadOnlyList<string> RelatedSearches { get; init; } = Array.Empty<string>();
        public IReadOnlyList<Product> TrendingProducts { get; init; } = Array.Empty<Product>();
    }

    public record AggregatedSearchResult
    {
        public SearchResults Products { get; init; } = new();
        public FacetResults Facets { get; init; } = new();
        public RecommendationResult Recommendations { get; init; } = new();
    }
}
