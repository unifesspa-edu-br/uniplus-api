namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Normalização referencial do envelope canônico (ADR-0109 D3).
/// </summary>
/// <remarks>
/// <para>
/// Só os ids <b>voláteis</b> — os Guid v7 que a entidade gera a cada execução — são
/// normalizados. Cada um vira um token distinto por ordem de primeira aparição
/// (<c>&lt;&lt;id-1&gt;&gt;</c>, <c>&lt;&lt;id-2&gt;&gt;</c>…), de modo que
/// <b>igualdade e referência são preservadas</b>: o <c>etapaRef</c> que aponta para
/// uma etapa continua apontando para o mesmo token que o <c>id</c> dela.
/// </para>
/// <para>
/// Os ids de <b>conteúdo</b> (oferta de curso, modalidade, documento — referências
/// cross-módulo, snapshot-copy) ficam <b>literais</b>. Zerar tudo indistintamente
/// tornaria a fixture cega justamente ao que ela deveria proteger: trocar
/// <c>modalidadeOrigemId</c> por <c>ofertaCursoOrigemId</c>, ou gravar
/// <see cref="Guid.Empty"/>, passaria despercebido.
/// </para>
/// </remarks>
internal static class NormalizadorDeIdsDoEnvelope
{
    private static readonly Regex GuidPattern = new(
        "(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Guid na forma "N" (32 hex, sem hífens) — como o nó de exigência do bloco
    /// <c>grafoDependencia</c> serializa a identidade da exigência (<c>EXIGENCIA/&lt;id:N&gt;</c>). Os
    /// <see cref="Regex"/> negados nas bordas impedem casar um trecho de 32 de dentro de um hash de 64
    /// hex — que também é conteúdo e nunca é um Guid.
    /// </summary>
    private static readonly Regex GuidFormaNPattern = new(
        "(?<![0-9a-fA-F])[0-9a-fA-F]{32}(?![0-9a-fA-F])",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private static readonly HashSet<Guid> IdsDeConteudo =
    [
        EnvelopeDeReferencia.OfertaCursoFixa,
        EnvelopeDeReferencia.ModalidadeFixa,
        EnvelopeDeReferencia.DocumentoFixo,
        EnvelopeDeReferencia.TipoDocumentoFixo,
    ];

    internal static string NormalizarIds(byte[] bytes)
    {
        // Token por IDENTIDADE de Guid (não por texto): o mesmo id de exigência aparece hifenado em
        // documentosExigidos.exigencias[].exigenciaId E na forma "N" no nó do grafo — as duas
        // aparições têm de virar o MESMO token para preservar a referência (ADR-0109 D3).
        Dictionary<Guid, string> tokens = [];

        // A forma hifenada primeiro (a ordem de aparição define a numeração dos tokens, estável entre
        // regenerações); depois a forma "N", que reusa o token do id já visto.
        string json = GuidPattern.Replace(Encoding.UTF8.GetString(bytes), match => Substituir(match, tokens));
        return GuidFormaNPattern.Replace(json, match => Substituir(match, tokens));
    }

    internal static string BytesNormalizados(JsonObject envelope) =>
        NormalizarIds(HashCanonicalComputer.ComputeSnapshotBytes(envelope));

    private static string Substituir(Match match, Dictionary<Guid, string> tokens)
    {
        if (!Guid.TryParse(match.Value, CultureInfo.InvariantCulture, out Guid id) || IdsDeConteudo.Contains(id))
        {
            return match.Value;
        }

        if (!tokens.TryGetValue(id, out string? token))
        {
            token = $"<<id-{tokens.Count + 1}>>";
            tokens[id] = token;
        }

        return token;
    }
}
