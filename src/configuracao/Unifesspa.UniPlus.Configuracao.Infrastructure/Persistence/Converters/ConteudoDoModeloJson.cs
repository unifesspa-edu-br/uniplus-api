namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O conteúdo do modelo de formulário num documento jsonb: lido e gravado inteiro, junto do modelo,
/// sem consulta por item (ADR-0137). As regras usam os mesmos tokens e a mesma forma de JSON do envelope
/// do processo, pelos serializadores de <c>Unifesspa.UniPlus.Regras</c>.
/// </summary>
internal static class ConteudoDoModeloJson
{
    public static readonly ValueConverter<ConteudoDoModelo, string> Converter =
        new(conteudo => Serializar(conteudo), json => Desserializar(json));

    public static readonly ValueComparer<ConteudoDoModelo> Comparer =
        new(
            (a, b) => Serializar(a!) == Serializar(b!),
            conteudo => Serializar(conteudo).GetHashCode(StringComparison.Ordinal),
            conteudo => Desserializar(Serializar(conteudo)));

    public static string Serializar(ConteudoDoModelo conteudo) => new JsonObject
    {
        ["titulo"] = conteudo.Titulo,
        ["etapas"] = new JsonArray([.. conteudo.Etapas.Select(static e => (JsonNode)new JsonObject
        {
            ["codigo"] = e.Codigo,
            ["ordem"] = e.Ordem,
            ["tipo"] = EstruturaFormulario.ParaToken(e.Tipo),
            ["bloco"] = EstruturaFormulario.ParaToken(e.Bloco),
            ["titulo"] = e.Titulo,
            ["descricao"] = e.Descricao,
            ["aviso"] = e.Aviso,
            ["exibicao"] = e.Exibicao is null ? null : PredicadoDnfJson.ParaJson(e.Exibicao),
        })]),
        ["itens"] = new JsonArray([.. conteudo.Itens.Select(static i => (JsonNode)CampoParaJson(i))]),
        ["termos"] = new JsonArray([.. conteudo.Termos.Select(static t => (JsonNode)new JsonObject
        {
            ["codigo"] = t.Codigo,
            ["ordem"] = t.Ordem,
            ["termoId"] = t.TermoId,
            ["versaoId"] = t.VersaoId,
            ["exibicao"] = t.Exibicao is null ? null : PredicadoDnfJson.ParaJson(t.Exibicao),
            ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(t.Obrigatoriedade),
        })]),
        ["pressupostos"] = new JsonArray([.. conteudo.Pressupostos.Select(static p => (JsonNode)p)]),
        ["grupos"] = new JsonArray([.. conteudo.Grupos.Select(static g => (JsonNode)new JsonObject
        {
            ["codigo"] = g.Codigo,
            ["ordem"] = g.Ordem,
            ["etapaCodigo"] = g.EtapaCodigo,
            ["rotulo"] = g.Rotulo,
            ["minimo"] = g.Minimo,
            ["maximo"] = g.Maximo,
            ["exibicao"] = g.Exibicao is null ? null : PredicadoDnfJson.ParaJson(g.Exibicao),
            ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(g.Obrigatoriedade),
            ["subitens"] = new JsonArray([.. g.Subitens.Select(static s => (JsonNode)CampoParaJson(s))]),
        })]),
    }.ToJsonString();

    private static JsonObject CampoParaJson(ItemDoModelo campo) => new()
    {
        ["fatoCodigo"] = campo.FatoCodigo,
        ["ordem"] = campo.Ordem,
        ["etapaCodigo"] = campo.EtapaCodigo,
        ["rotulo"] = campo.Rotulo,
        ["tipoRenderizacao"] = campo.TipoRenderizacao.ToCodigo(),
        ["formato"] = campo.Formato,
        ["ajuda"] = campo.Ajuda,
        ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(campo.Obrigatoriedade),
        ["exibicao"] = campo.Exibicao is null ? null : PredicadoDnfJson.ParaJson(campo.Exibicao),
        ["restricoes"] = RestricaoValorJson.ParaJson(campo.Restricoes),
        ["pedirConfirmacao"] = campo.PedirConfirmacao,
    };

    /// <summary>
    /// Remonta o conteúdo gravado. O documento só é escrito por <see cref="Serializar"/>, sobre um
    /// modelo que passou pelas regras; uma forma que não remonta é dado corrompido, e por isso lança.
    /// </summary>
    public static ConteudoDoModelo Desserializar(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        JsonElement raiz = documento.RootElement;
        return new ConteudoDoModelo(
            Texto(raiz, "titulo"),
            [.. raiz.GetProperty("etapas").EnumerateArray().Select(static e => new EtapaDoModelo(
                e.GetProperty("codigo").GetString()!,
                e.GetProperty("ordem").GetInt32(),
                EstruturaFormulario.TipoDoToken(e.GetProperty("tipo").GetString()),
                EstruturaFormulario.BlocoDoToken(Texto(e, "bloco")),
                e.GetProperty("titulo").GetString()!,
                Texto(e, "descricao"),
                Texto(e, "aviso"),
                Predicado(e, "exibicao")))],
            [.. raiz.GetProperty("itens").EnumerateArray().Select(CampoDeJson)],
            [.. raiz.GetProperty("termos").EnumerateArray().Select(static t => new TermoDoModelo(
                t.GetProperty("codigo").GetString()!,
                t.GetProperty("ordem").GetInt32(),
                t.GetProperty("termoId").GetGuid(),
                t.GetProperty("versaoId").GetGuid(),
                Predicado(t, "exibicao"),
                Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(t.GetProperty("obrigatoriedade")))))],
            [.. raiz.GetProperty("pressupostos").EnumerateArray().Select(static p => p.GetString()!)],
            [.. raiz.GetProperty("grupos").EnumerateArray().Select(static g => new GrupoDoModelo(
                g.GetProperty("codigo").GetString()!,
                g.GetProperty("ordem").GetInt32(),
                Texto(g, "etapaCodigo"),
                g.GetProperty("rotulo").GetString()!,
                g.GetProperty("minimo").GetInt32(),
                Inteiro(g, "maximo"),
                Predicado(g, "exibicao"),
                Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(g.GetProperty("obrigatoriedade"))),
                [.. g.GetProperty("subitens").EnumerateArray().Select(CampoDeJson)]))]);
    }

    private static ItemDoModelo CampoDeJson(JsonElement campo) => new(
        campo.GetProperty("fatoCodigo").GetString()!,
        campo.GetProperty("ordem").GetInt32(),
        Texto(campo, "etapaCodigo"),
        campo.GetProperty("rotulo").GetString()!,
        TipoRenderizacaoCodigo.FromCodigo(campo.GetProperty("tipoRenderizacao").GetString()),
        Texto(campo, "formato"),
        Texto(campo, "ajuda"),
        Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(campo.GetProperty("obrigatoriedade"))),
        Predicado(campo, "exibicao"),
        Remontar(RestricaoValorJson.ListaDeJson(campo.GetProperty("restricoes"))),
        campo.GetProperty("pedirConfirmacao").GetBoolean());

    private static int? Inteiro(JsonElement objeto, string chave) =>
        objeto.TryGetProperty(chave, out JsonElement valor) && valor.ValueKind == JsonValueKind.Number ? valor.GetInt32() : null;

    private static string? Texto(JsonElement objeto, string chave) =>
        objeto.TryGetProperty(chave, out JsonElement valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static PredicadoDnf? Predicado(JsonElement objeto, string chave) =>
        objeto.TryGetProperty(chave, out JsonElement valor) && valor.ValueKind != JsonValueKind.Null
            ? Remontar(PredicadoDnfJson.DeJson(valor))
            : null;

    private static T Remontar<T>(Kernel.Results.Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"O conteúdo gravado do modelo de formulário não remonta: {resultado.Error?.Message}");
}
