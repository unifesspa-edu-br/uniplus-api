namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
        ["paginas"] = new JsonArray([.. conteudo.Paginas.Select(PaginaParaJson)]),
        ["itens"] = new JsonArray([.. conteudo.Itens.Select(CampoParaJson)]),
        ["termos"] = new JsonArray([.. conteudo.Termos.Select(TermoParaJson)]),
        ["pressupostos"] = new JsonArray([.. conteudo.Pressupostos.Select(static pressuposto => (JsonNode)pressuposto)]),
        ["grupos"] = new JsonArray([.. conteudo.Grupos.Select(GrupoParaJson)]),
        ["documentosExigidos"] = new JsonArray([.. conteudo.Documentos.DocumentosExigidos.Select(DocumentoParaJson)]),
        ["gruposDeDocumentos"] = new JsonArray([.. conteudo.Documentos.GruposDeDocumentos.Select(GrupoDeDocumentosParaJson)]),
    }.ToJsonString();

    private static JsonObject PaginaParaJson(PaginaDoModelo pagina) => new()
    {
        ["codigo"] = pagina.Codigo,
        ["ordem"] = pagina.Ordem,
        ["tipo"] = pagina.Tipo.Codigo(),
        ["bloco"] = pagina.Bloco?.Codigo(),
        ["titulo"] = pagina.Titulo,
        ["descricao"] = pagina.Descricao,
        ["aviso"] = pagina.Aviso,
        ["exibicao"] = PredicadoOpcionalParaJson(pagina.Exibicao),
    };

    private static JsonObject CampoParaJson(ItemDoModelo campo) => new()
    {
        ["codigoDoDado"] = campo.FatoCodigo,
        ["ordem"] = campo.Ordem,
        ["paginaCodigo"] = campo.PaginaCodigo,
        ["rotulo"] = campo.Apresentacao.Rotulo,
        ["tipoDeCampo"] = campo.Apresentacao.TipoDeCampo.Codigo(),
        ["formato"] = campo.Formato,
        ["ajuda"] = campo.Apresentacao.Ajuda,
        ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(campo.Regras.Obrigatoriedade),
        ["exibicao"] = PredicadoOpcionalParaJson(campo.Regras.Exibicao),
        ["restricoes"] = RestricaoValorJson.ParaJson(campo.Regras.Restricoes),
        ["pedirConfirmacao"] = campo.Apresentacao.PedirConfirmacao,
        ["impedimento"] = campo.Regras.Impedimento is null ? null : PredicadoDnfJson.ParaJson(campo.Regras.Impedimento),
    };

    private static JsonObject TermoParaJson(TermoDoModelo termo) => new()
    {
        ["codigo"] = termo.Codigo,
        ["ordem"] = termo.Ordem,
        ["termoId"] = termo.TermoId,
        ["versaoId"] = termo.VersaoId,
        ["exibicao"] = PredicadoOpcionalParaJson(termo.Regras.Exibicao),
        ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(termo.Regras.Obrigatoriedade),
    };

    private static JsonObject GrupoParaJson(GrupoDoModelo grupo) => new()
    {
        ["codigo"] = grupo.Codigo,
        ["ordem"] = grupo.Ordem,
        ["paginaCodigo"] = grupo.PaginaCodigo,
        ["rotulo"] = grupo.Rotulo,
        ["minimo"] = grupo.Ocorrencias.Minimo,
        ["maximo"] = grupo.Ocorrencias.Maximo,
        ["incluiCandidato"] = grupo.Ocorrencias.IncluiCandidato,
        ["exibicao"] = PredicadoOpcionalParaJson(grupo.Regras.Exibicao),
        ["obrigatoriedade"] = PredicadoDnfJson.ParaJson(grupo.Regras.Obrigatoriedade),
        ["subitens"] = new JsonArray([.. grupo.Subitens.Select(CampoParaJson)]),
    };

    private static JsonObject GrupoDeDocumentosParaJson(GrupoDeDocumentosDoModelo grupo) => new()
    {
        ["quantidadeMinima"] = grupo.QuantidadeMinima,
        ["consequenciaIndeferimento"] = grupo.Indeferimento?.Consequencia,
        ["basesLegais"] = new JsonArray([.. (grupo.Indeferimento?.BasesLegais ?? []).Select(BaseLegalParaJson)]),
        ["documentos"] = new JsonArray([.. grupo.Documentos.Select(DocumentoParaJson)]),
    };

    private static JsonObject DocumentoParaJson(DocumentoExigidoDoModelo documento) => new()
    {
        ["faseCodigo"] = documento.FaseCodigo,
        ["tipoDocumentoCodigo"] = documento.TipoDocumentoCodigo,
        ["obrigatorio"] = documento.Indeferimento.Obrigatorio,
        ["quando"] = PredicadoOpcionalParaJson(documento.Quando),
        ["consequenciaIndeferimento"] = documento.Indeferimento.Consequencia,
        ["basesLegais"] = new JsonArray([.. documento.Indeferimento.BasesLegais.Select(BaseLegalParaJson)]),
        ["prazoDeEmissao"] = PrazoParaJson(documento.Requisitos.PrazoDeEmissao),
        ["validade"] = ValidadeParaJson(documento.Requisitos.Validade),
        ["porOpcao"] = PorOpcaoParaJson(documento.PorOpcao),
    };

    private static JsonObject? PrazoParaJson(PrazoDeEmissaoDoModelo? prazo) => prazo is null ? null : new()
    {
        ["escolha"] = prazo.Escolha.Codigo(),
        ["valor"] = prazo.Valor,
        ["unidade"] = prazo.Unidade?.Codigo(),
        ["referencia"] = ReferenciaParaJson(prazo.Referencia),
        ["efeitoForaDoPrazo"] = prazo.EfeitoForaDoPrazo?.Codigo(),
    };

    private static JsonObject? ValidadeParaJson(ValidadeDoDocumentoDoModelo? validade) => validade is null ? null : new()
    {
        ["escolha"] = validade.Escolha.Codigo(),
        ["referencia"] = ReferenciaParaJson(validade.Referencia),
        ["efeitoDoVencimento"] = validade.EfeitoDoVencimento?.Codigo(),
    };

    private static JsonObject? ReferenciaParaJson(DataDeReferenciaDoDocumentoDoModelo? referencia) => referencia is null ? null : new()
    {
        ["tipo"] = referencia.Tipo.Codigo(),
        ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["faseCodigo"] = referencia.FaseCodigo,
    };

    private static JsonObject BaseLegalParaJson(BaseLegalDoModelo baseLegal) => new()
    {
        ["referencia"] = baseLegal.Referencia,
        ["abrangencia"] = baseLegal.Abrangencia,
        ["situacao"] = baseLegal.Situacao,
        ["observacao"] = baseLegal.Observacao,
    };

    private static JsonObject? PorOpcaoParaJson(ExigenciaPorOpcao? porOpcao) =>
        porOpcao is null
            ? null
            : new JsonObject
            {
                ["codigoDoDado"] = porOpcao.FatoCodigo,
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
        JsonElement elementoPrincipal = documento.RootElement;
        return new ConteudoDoModelo(
            Texto(elementoPrincipal, "titulo"),
            [.. elementoPrincipal.GetProperty("paginas").EnumerateArray().Select(PaginaDeJson)],
            [.. elementoPrincipal.GetProperty("itens").EnumerateArray().Select(CampoDeJson)],
            [.. elementoPrincipal.GetProperty("termos").EnumerateArray().Select(TermoDeJson)],
            [.. elementoPrincipal.GetProperty("pressupostos").EnumerateArray().Select(static pressuposto => pressuposto.GetString()!)],
            [.. elementoPrincipal.GetProperty("grupos").EnumerateArray().Select(GrupoDeJson)],
            new ConjuntoDeDocumentosDoModelo(DocumentosDeJson(elementoPrincipal), GruposDeDocumentosDeJson(elementoPrincipal)));
    }

    private static PaginaDoModelo PaginaDeJson(JsonElement pagina) => new(
        pagina.GetProperty("codigo").GetString()!,
        pagina.GetProperty("ordem").GetInt32(),
        CatalogoDe<TipoPaginaFormulario>.Obter(pagina.GetProperty("tipo").GetString()!),
        Texto(pagina, "bloco") is { } bloco ? CatalogoDe<BlocoSistema>.Obter(bloco) : null,
        pagina.GetProperty("titulo").GetString()!,
        Texto(pagina, "descricao"),
        Texto(pagina, "aviso"),
        Predicado(pagina, "exibicao"));

    private static ItemDoModelo CampoDeJson(JsonElement campo) => new(
        campo.GetProperty("codigoDoDado").GetString()!,
        campo.GetProperty("ordem").GetInt32(),
        TextoOuNulo(campo.GetProperty("paginaCodigo")),
        ApresentacaoDeJson(campo),
        Texto(campo, "formato"),
        RegrasDeJson(campo));

    private static ApresentacaoDoCampo ApresentacaoDeJson(JsonElement campo) => new(
        campo.GetProperty("rotulo").GetString()!,
        CatalogoDe<TipoDeCampo>.Obter(campo.GetProperty("tipoDeCampo").GetString()!),
        Texto(campo, "ajuda"),
        campo.GetProperty("pedirConfirmacao").GetBoolean());

    private static RegrasDoCampo RegrasDeJson(JsonElement campo) => new(
        Predicado(campo, "exibicao"),
        Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(campo.GetProperty("obrigatoriedade"))),
        Remontar(RestricaoValorJson.ListaDeJson(campo.GetProperty("restricoes"))),
        Remontar(PredicadoDnfJson.ImpedimentoDeJson(campo.TryGetProperty("impedimento", out JsonElement impedimento) ? impedimento : default)));

    private static TermoDoModelo TermoDeJson(JsonElement termo) => new(
        termo.GetProperty("codigo").GetString()!,
        termo.GetProperty("ordem").GetInt32(),
        termo.GetProperty("termoId").GetGuid(),
        termo.GetProperty("versaoId").GetGuid(),
        new ExibicaoEObrigatoriedade(
            Predicado(termo, "exibicao"), Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(termo.GetProperty("obrigatoriedade")))));

    private static GrupoDoModelo GrupoDeJson(JsonElement grupo) => new(
        grupo.GetProperty("codigo").GetString()!,
        grupo.GetProperty("ordem").GetInt32(),
        TextoOuNulo(grupo.GetProperty("paginaCodigo")),
        grupo.GetProperty("rotulo").GetString()!,
        new OcorrenciasDoGrupo(
            grupo.GetProperty("minimo").GetInt32(), InteiroOuNulo(grupo.GetProperty("maximo")), grupo.GetProperty("incluiCandidato").GetBoolean()),
        new ExibicaoEObrigatoriedade(
            Predicado(grupo, "exibicao"), Remontar(PredicadoDnfJson.ObrigatoriedadeDeJson(grupo.GetProperty("obrigatoriedade")))),
        [.. grupo.GetProperty("subitens").EnumerateArray().Select(CampoDeJson)]);

    private static IReadOnlyList<DocumentoExigidoDoModelo> DocumentosDeJson(JsonElement elementoPrincipal) =>
        elementoPrincipal.TryGetProperty("documentosExigidos", out JsonElement documentos)
            ? [.. documentos.EnumerateArray().Select(DocumentoDeJson)]
            : [];

    // Chave ausente é "nenhum grupo".
    private static IReadOnlyList<GrupoDeDocumentosDoModelo> GruposDeDocumentosDeJson(JsonElement elementoPrincipal) =>
        elementoPrincipal.TryGetProperty("gruposDeDocumentos", out JsonElement grupos)
            ? [.. grupos.EnumerateArray().Select(GrupoDeDocumentosDeJson)]
            : [];

    private static GrupoDeDocumentosDoModelo GrupoDeDocumentosDeJson(JsonElement grupo) => new(
        InteiroOuNulo(grupo.GetProperty("quantidadeMinima")),
        Texto(grupo, "consequenciaIndeferimento") is { } consequencia
            ? new IndeferimentoDoGrupoDeDocumentosDoModelo(consequencia, [.. grupo.GetProperty("basesLegais").EnumerateArray().Select(BaseLegalDeJson)])
            : null,
        [.. grupo.GetProperty("documentos").EnumerateArray().Select(DocumentoDeJson)]);

    private static DocumentoExigidoDoModelo DocumentoDeJson(JsonElement documento) => new(
        documento.GetProperty("faseCodigo").GetString()!,
        documento.GetProperty("tipoDocumentoCodigo").GetString()!,
        Predicado(documento, "quando"),
        new IndeferimentoDoDocumentoDoModelo(
            documento.GetProperty("obrigatorio").GetBoolean(),
            documento.GetProperty("consequenciaIndeferimento").GetString()!,
            [.. documento.GetProperty("basesLegais").EnumerateArray().Select(BaseLegalDeJson)]),
        new RequisitosDoDocumentoDoModelo(PrazoDeJson(documento), ValidadeDeJson(documento)),
        PorOpcaoDeJson(documento));

    // Chave ausente ou nula é "ainda não escolhido".
    private static PrazoDeEmissaoDoModelo? PrazoDeJson(JsonElement documento) =>
        Objeto(documento, "prazoDeEmissao") is { } prazo
            ? new PrazoDeEmissaoDoModelo(
                CatalogoDe<EscolhaDoPrazoDeEmissao>.Obter(prazo.GetProperty("escolha").GetString()!),
                InteiroOuNulo(prazo.GetProperty("valor")),
                CodigoOpcional<UnidadeDoPrazoDeEmissao>(prazo, "unidade"),
                ReferenciaDeJson(prazo),
                CodigoOpcional<EfeitoNaAnaliseDoDocumento>(prazo, "efeitoForaDoPrazo"))
            : null;

    private static ValidadeDoDocumentoDoModelo? ValidadeDeJson(JsonElement documento) =>
        Objeto(documento, "validade") is { } validade
            ? new ValidadeDoDocumentoDoModelo(
                CatalogoDe<EscolhaDaValidade>.Obter(validade.GetProperty("escolha").GetString()!),
                ReferenciaDeJson(validade),
                CodigoOpcional<EfeitoNaAnaliseDoDocumento>(validade, "efeitoDoVencimento"))
            : null;

    private static DataDeReferenciaDoDocumentoDoModelo? ReferenciaDeJson(JsonElement pai) =>
        Objeto(pai, "referencia") is { } referencia
            ? new DataDeReferenciaDoDocumentoDoModelo(
                CatalogoDe<TipoDaDataDeReferencia>.Obter(referencia.GetProperty("tipo").GetString()!),
                Texto(referencia, "faseCodigo"),
                Texto(referencia, "data") is { } data ? DateOnly.ParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null)
            : null;

    private static TEnum? CodigoOpcional<TEnum>(JsonElement objeto, string chave)
        where TEnum : struct, Enum =>
        Texto(objeto, chave) is { } codigo ? CatalogoDe<TEnum>.Obter(codigo) : null;

    private static JsonElement? Objeto(JsonElement pai, string chave) =>
        pai.TryGetProperty(chave, out JsonElement valor) && valor.ValueKind == JsonValueKind.Object ? valor : null;

    private static BaseLegalDoModelo BaseLegalDeJson(JsonElement baseLegal) => new(
        baseLegal.GetProperty("referencia").GetString()!,
        baseLegal.GetProperty("abrangencia").GetString()!,
        baseLegal.GetProperty("situacao").GetString()!,
        Texto(baseLegal, "observacao"));

    private static ExigenciaPorOpcao? PorOpcaoDeJson(JsonElement documento) =>
        documento.TryGetProperty("porOpcao", out JsonElement porOpcao) && porOpcao.ValueKind == JsonValueKind.Object
            ? new ExigenciaPorOpcao(
                porOpcao.GetProperty("codigoDoDado").GetString()!,
                [.. porOpcao.GetProperty("exceto").EnumerateArray().Select(static valor => valor.GetString()!)])
            : null;

    /// <summary>Só o nulo explícito é ausência; qualquer outro valor que não seja inteiro falha, como o resto da leitura.</summary>
    private static int? InteiroOuNulo(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.Null ? null : valor.GetInt32();

    private static string? TextoOuNulo(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.Null ? null : valor.GetString();

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
