namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Text;

/// <summary>
/// Constrói o valor do header <c>Link</c> (RFC 5988/8288) a partir de
/// <see cref="PageLinks"/>: rels ausentes (<c>next</c>/<c>prev</c>)
/// são omitidas; <c>self</c> está sempre presente.
/// </summary>
public static class LinkHeaderBuilder
{
    public static string Build(PageLinks links)
    {
        ArgumentNullException.ThrowIfNull(links);

        StringBuilder builder = new();
        AppendIfPresent(builder, links.Prev, "prev");
        AppendIfPresent(builder, links.Next, "next");
        AppendIfPresent(builder, links.Self, "self");
        return builder.ToString();
    }

    private static void AppendIfPresent(StringBuilder builder, string? url, string rel)
    {
        if (string.IsNullOrEmpty(url))
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append('<').Append(url).Append(">; rel=\"").Append(rel).Append('"');
    }
}
