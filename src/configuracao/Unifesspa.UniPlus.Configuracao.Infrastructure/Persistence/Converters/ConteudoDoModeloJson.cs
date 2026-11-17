namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
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
        ["etapas"] = new JsonArray([.. conteudo.Etapas.Select(EtapaParaJson)]),
        ["itens"] = new JsonArray([.. conteudo.Itens.Select(CampoParaJson)]),
        ["termos"] = new JsonArray([.. conteudo.Termos.Select(TermoParaJson)]),
        ["pressupostos"] = new JsonArray([.. conteudo.Pressupostos.Select(static pressuposto => (JsonNode)pressuposto)]),
        ["grupos"] = new JsonArray([.. conteudo.Grupos.Select(GrupoParaJson)]),
        ["documentosExigidos"] = new JsonArray([.. conteudo.Documentos.Select(DocumentoParaJson)]),
    }.ToJsonString();

    private static JsonObject EtapaParaJson(EtapaDoModelo etapa) => new()
    {
        ["codigo"] = etapa.Codigo,
        ["ordem"] = etapa.Ordem,
        ["tipo"] = EstruturaFormulario.ParaToken(etapa.Tipo),
        ["bloco"] = EstruturaFormulario.ParaToken(etapa.Bloco),
        ["titulo"] = etapa.Titulo,
        ["descricao"] = etapa.Descricao,
        ["aviso"] = etapa.Aviso,
        ["exibicao"] = PredicadoOpcionalParaJson(etapa.Exibicao),
    };

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
        ["exibicao"] = PredicadoOpcionalParaJson(campo.Exibicao),
        ["restricoes"] = RestricaoValorJson.ParaJson(campo.Restricoes),
        ["pedirConfirmacao"] = campo.PedirConfirmacao,
        ["impedimento"] = campo.Impedimento is null ? null : PredicadoDnfJson.ParaJson(campo.Impedimento),
    };

    private static JsonObject TermoParaJson(TermoDoModelo termo) => new()
    {
        ["codigo"] = termo.Codigo,
        ["ordem"] = termo.Ordem,
        ["termoId"] = termo.TermoId,
        ["versaoId"] = termo.VersaoId,
        ["exibicao"] = PredicadoOpcionalParaJson(termo.Exibicao),
        ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(termo.Obrigatoriedade),
    };

    private static JsonObject GrupoParaJson(GrupoDoModelo grupo) => new()
    {
        ["codigo"] = grupo.Codigo,
        ["ordem"] = grupo.Ordem,
        ["etapaCodigo"] = grupo.EtapaCodigo,
        ["rotulo"] = grupo.Rotulo,
        ["minimo"] = grupo.Minimo,
        ["maximo"] = grupo.Maximo,
        ["incluiCandidato"] = grupo.IncluiCandidato,
        ["exibicao"] = PredicadoOpcionalParaJson(grupo.Exibicao),
        ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(grupo.Obrigatoriedade),
        ["subitens"] = new JsonArray([.. grupo.Subitens.Select(CampoParaJson)]),
    };

    private static JsonObject DocumentoParaJson(DocumentoExigidoDoModelo documento) => new()
    {
        ["faseCodigo"] = documento.FaseCodigo,
        ["tipoDocumentoCodigo"] = documento.TipoDocumentoCodigo,
        ["obrigatorio"] = documento.Obrigatorio,
        ["quando"] = PredicadoOpcionalParaJson(documento.Quando),
        ["consequenciaIndeferimento"] = documento.ConsequenciaIndeferimento,
        ["basesLegais"] = new JsonArray([.. documento.BasesLegais.Select(BaseLegalParaJson)]),
        ["porOpcao"] = PorOpcaoParaJson(documento.PorOpcao),
    };

    private static JsonObject BaseLegalParaJson(BaseLegalDoDocumentoDoModelo baseLegal) => new()
    {
        ["referencia"] = baseLegal.Referencia,
        ["abrangencia"] = baseLegal.Abrangencia,
        ["status"] = baseLegal.Status,
        ["observacao"] = baseLegal.Observacao,
    };

    private static JsonObject? PorOpcaoParaJson(ExigenciaPorOpcao? porOpcao) =>
        porOpcao is null
            ? null
            : new JsonObject
            {
                ["fatoCodigo"] = porOpcao.FatoCodigo,
                ["exceto"] = new JsonArray([.. porOpcao.Exceto.Select(static valor => (JsonNode)JsonValue.Create(valor))]),
            };

    private static JsonArray? PredicadoOpcionalParaJson(PredicadoDnf? predicado) =>
        predicado is null ? null : PredicadoDnfJson.ParaJson(predicado);

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
            [.. raiz.GetProperty("etapas").EnumerateArray().Select(EtapaDeJson)],
            [.. raiz.GetProperty("itens").EnumerateArray().Select(CampoDeJson)],
            [.. raiz.GetProperty("termos").EnumerateArray().Select(TermoDeJson)],
            [.. raiz.GetProperty("pressupostos").EnumerateArray().Select(static pressuposto => pressuposto.GetString()!)],
            [.. raiz.GetProperty("grupos").EnumerateArray().Select(GrupoDeJson)],
            DocumentosDeJson(raiz));
    }

    private static EtapaDoModelo EtapaDeJson(JsonElement etapa) => new(
        etapa.GetProperty("codigo").GetString()!,
        etapa.GetProperty("ordem").GetInt32(),
        EstruturaFormulario.TipoDoToken(etapa.GetProperty("tipo").GetString()),
        EstruturaFormulario.BlocoDoToken(Texto(etapa, "bloco")),
        etapa.GetProperty("titulo").GetString()!,
        Texto(etapa, "descricao"),
        Texto(etapa, "aviso"),
        Predicado(etapa, "exibicao"));

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
        campo.GetProperty("pedirConfirmacao").GetBoolean(),
        Remontar(PredicadoDnfJson.ImpedimentoDeJson(campo.TryGetProperty("impedimento", out JsonElement impedimento) ? impedimento : default)));

    private static TermoDoModelo TermoDeJson(JsonElement termo) => new(
        termo.GetProperty("codigo").GetString()!,
        termo.GetProperty("ordem").GetInt32(),
        termo.GetProperty("termoId").GetGuid(),
        termo.GetProperty("versaoId").GetGuid(),
        Predicado(termo, "exibicao"),
        Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(termo.GetProperty("obrigatoriedade"))));

    private static GrupoDoModelo GrupoDeJson(JsonElement grupo) => new(
        grupo.GetProperty("codigo").GetString()!,
        grupo.GetProperty("ordem").GetInt32(),
        Texto(grupo, "etapaCodigo"),
        grupo.GetProperty("rotulo").GetString()!,
        grupo.GetProperty("minimo").GetInt32(),
        InteiroOuNulo(grupo.GetProperty("maximo")),
        Predicado(grupo, "exibicao"),
        Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(grupo.GetProperty("obrigatoriedade"))),
        [.. grupo.GetProperty("subitens").EnumerateArray().Select(CampoDeJson)],
        grupo.GetProperty("incluiCandidato").GetBoolean());

    private static IReadOnlyList<DocumentoExigidoDoModelo> DocumentosDeJson(JsonElement raiz) =>
        raiz.TryGetProperty("documentosExigidos", out JsonElement documentos)
            ? [.. documentos.EnumerateArray().Select(DocumentoDeJson)]
            : [];

    private static DocumentoExigidoDoModelo DocumentoDeJson(JsonElement documento) => new(
        documento.GetProperty("faseCodigo").GetString()!,
        documento.GetProperty("tipoDocumentoCodigo").GetString()!,
        documento.GetProperty("obrigatorio").GetBoolean(),
        Predicado(documento, "quando"),
        documento.GetProperty("consequenciaIndeferimento").GetString()!,
        [.. documento.GetProperty("basesLegais").EnumerateArray().Select(BaseLegalDeJson)],
        PorOpcaoDeJson(documento));

    private static BaseLegalDoDocumentoDoModelo BaseLegalDeJson(JsonElement baseLegal) => new(
        baseLegal.GetProperty("referencia").GetString()!,
        baseLegal.GetProperty("abrangencia").GetString()!,
        baseLegal.GetProperty("status").GetString()!,
        Texto(baseLegal, "observacao"));

    private static ExigenciaPorOpcao? PorOpcaoDeJson(JsonElement documento) =>
        documento.TryGetProperty("porOpcao", out JsonElement porOpcao) && porOpcao.ValueKind == JsonValueKind.Object
            ? new ExigenciaPorOpcao(
                porOpcao.GetProperty("fatoCodigo").GetString()!,
                [.. porOpcao.GetProperty("exceto").EnumerateArray().Select(static valor => valor.GetString()!)])
            : null;

    /// <summary>Só o nulo explícito é ausência; qualquer outro valor que não seja inteiro falha, como o resto da leitura.</summary>
    private static int? InteiroOuNulo(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.Null ? null : valor.GetInt32();

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
