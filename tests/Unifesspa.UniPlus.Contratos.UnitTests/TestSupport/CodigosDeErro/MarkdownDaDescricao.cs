namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport.CodigosDeErro;

using System.Text.RegularExpressions;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

internal static partial class MarkdownDaDescricao
{
    private static readonly MarkdownPipeline Formato = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    internal static IEnumerable<string> Conferir(string bloco)
    {
        MarkdownDocument documento = Markdown.Parse(bloco, Formato);
        if (documento.Count != 1 || documento[0] is not (ParagraphBlock or ListBlock or Table or FencedCodeBlock))
            yield return "bloco fora do Markdown simples";
        if (documento.Descendants<HeadingBlock>().Any())
            yield return "título";
        if (TemMarcacaoEmbutida(documento, bloco))
            yield return "HTML ou MDX";
        if (documento.Descendants<QuoteBlock>().Any() || documento.Descendants<CodeBlock>().Any(static codigo => codigo is not FencedCodeBlock)
            || (documento.Descendants<FencedCodeBlock>().Any() && !BlocoCercado().IsMatch(bloco)))
            yield return "citação ou bloco de código";
        if (TemLinkInvalido(documento))
            yield return "link fora de HTTPS ou do portal";
    }

    internal static string TextoVisivel(string bloco) => Markdown.ToPlainText(bloco, Formato);

    internal static IEnumerable<string> CodigosDeErroVinculados(string bloco) =>
        Markdown.Parse(bloco, Formato).Descendants<LinkInline>()
            .Where(static link => !link.IsImage && link.Url is not null && link.Url.StartsWith("/erros/", StringComparison.Ordinal))
            .Select(static link => link.Url!["/erros/".Length..].Split('?', '#')[0].TrimEnd('/'))
            .Where(static codigo => codigo.Length > 0);

    internal static bool TemTermoDeImplementacao(string bloco)
    {
        string semCodigo = CodigoEmLinhaParaLinguagem().Replace(BlocoCercado().Replace(bloco, string.Empty), string.Empty);
        return TermosDeImplementacao().IsMatch(Markdown.ToPlainText(semCodigo, Formato));
    }

    [GeneratedRegex(@"\b(?:payloads?|checagem|checagens|agregados?|endpoints?|envelopes?|snapshots?|handlers?|value objects?|codecs?|guids?|quer(?:y|ies)|enums?|uuids?|bugs?|validators?|janelas?|(?:âncor|ancor)\w*|port(?:ão|ões)|gates?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TermosDeImplementacao();

    [GeneratedRegex(@"(?<!\\)(?<crases>`+)(?!`)[\s\S]*?(?<!`)\k<crases>(?!`)", RegexOptions.CultureInvariant)]
    private static partial Regex CodigoEmLinhaParaLinguagem();

    private static bool TemMarcacaoEmbutida(MarkdownDocument documento, string bloco) =>
        documento.Descendants<HtmlInline>().Any() || documento.Descendants<HtmlBlock>().Any()
        || ExpressaoMdx().IsMatch(BlocoCercado().Replace(SemCodigoEmLinha(documento, bloco), string.Empty));

    private static bool TemLinkInvalido(MarkdownDocument documento) =>
        documento.Descendants<LinkInline>().Any(static link => link.IsImage || !DestinoPermitido(link.Url))
        || documento.Descendants<AutolinkInline>().Any(static link => link.IsEmail || !DestinoPermitido(link.Url));

    [GeneratedRegex(@"^\s*(?:import\s+.*\s+from|export\s+(?:const|default))\b|\{[^{}]*\}", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ExpressaoMdx();

    [GeneratedRegex(@"^```(?:json|text)?[ \t]*\n[\s\S]*?^```[ \t]*$", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex BlocoCercado();

    private static string SemCodigoEmLinha(MarkdownDocument documento, string bloco)
    {
        char[] texto = bloco.ToCharArray();
        foreach (CodeInline codigo in documento.Descendants<CodeInline>())
            Array.Fill(texto, ' ', codigo.Span.Start, codigo.Span.Length);
        return new string(texto);
    }

    private static bool DestinoPermitido(string? destino) =>
        destino is not null && !destino.Any(static caractere => caractere is '\\' || char.IsControl(caractere))
        && (CaminhoDoPortal(destino) || EnderecoHttps(destino));

    private static bool CaminhoDoPortal(string destino) =>
        destino.StartsWith('/') && !destino.StartsWith("//", StringComparison.Ordinal);

    private static bool EnderecoHttps(string destino) =>
        destino.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        && Uri.TryCreate(destino, UriKind.Absolute, out Uri? uri)
        && uri.Host.Length > 0;
}
