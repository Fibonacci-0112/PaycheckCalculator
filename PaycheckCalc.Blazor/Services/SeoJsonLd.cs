using System.Text.Json;

namespace PaycheckCalc.Blazor.Services;

/// <summary>A single frequently-asked question and its answer, used for FAQPage structured data.</summary>
public record FaqItem(string Question, string Answer);

/// <summary>
/// Builds schema.org JSON-LD structured data for embedding in a &lt;script type="application/ld+json"&gt; block.
/// Serialization goes through System.Text.Json, whose default encoder escapes &lt;, &gt; and &amp; to \uXXXX —
/// so user-facing strings (titles, descriptions, blurbs) are safe to embed inside a script tag.
/// </summary>
public static class SeoJsonLd
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>
    /// Builds a combined JSON-LD <c>@graph</c> with a WebApplication node and, when FAQs are supplied,
    /// a FAQPage node for rich results.
    /// </summary>
    public static string Build(string name, string description, string url,
        IReadOnlyList<FaqItem>? faqs = null)
    {
        var graph = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "WebApplication",
                ["name"] = name,
                ["description"] = description,
                ["url"] = url,
                ["applicationCategory"] = "FinanceApplication",
                ["operatingSystem"] = "Web",
                ["offers"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["price"] = "0",
                    ["priceCurrency"] = "USD"
                }
            }
        };

        if (faqs is { Count: > 0 })
        {
            graph.Add(new Dictionary<string, object?>
            {
                ["@type"] = "FAQPage",
                ["mainEntity"] = faqs.Select(f => new Dictionary<string, object?>
                {
                    ["@type"] = "Question",
                    ["name"] = f.Question,
                    ["acceptedAnswer"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Answer",
                        ["text"] = f.Answer
                    }
                }).ToList()
            });
        }

        var root = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        };

        return JsonSerializer.Serialize(root, Options);
    }
}
