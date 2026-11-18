namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// Todo descritivo e todo valor derivado que a exportação traz ao lado do código é só de leitura: o esquema o marca como
/// <c>readOnly</c> e a descrição diz que a importação o ignora e que a referência é o código.
/// </summary>
public sealed class CamposSoDeLeituraDoEsquemaDaConfiguracaoTests
{
    private const string ArquivoDoDocumento = "documento.schema.json";

    [Theory(DisplayName = "O campo só de leitura é marcado readOnly e a descrição diz que a importação o ignora")]
    [InlineData(ArquivoDoDocumento, "properties/id")]
    [InlineData(ArquivoDoDocumento, "properties/versao")]
    [InlineData(ArquivoDoDocumento, "properties/publicadoEm")]
    [InlineData(ArquivoDoDocumento, "properties/tiposDeAtoCongelados")]
    [InlineData("identificacao.schema.json", "properties/localidadeNome")]
    [InlineData("identificacao.schema.json", "properties/localidadeUf")]
    [InlineData("identificacao.schema.json", "properties/tipoProcessoNome")]
    [InlineData("identificacao.schema.json", "properties/unidadeAdministradoraNome")]
    [InlineData("identificacao.schema.json", "properties/unidadeAdministradoraSigla")]
    [InlineData("cronograma.schema.json", "$defs/fase/properties/unidadeResponsavelNome")]
    [InlineData("cronograma.schema.json", "$defs/fase/properties/unidadeResponsavelSigla")]
    [InlineData("divulgacao.schema.json", "properties/regraNomeAbreviado")]
    [InlineData("motivo.schema.json", "properties/descricao")]
    [InlineData("motivo.schema.json", "properties/subclassificacao")]
    [InlineData("motivo.schema.json", "properties/sentido")]
    [InlineData("motivo.schema.json", "properties/versaoDoEditalDeEntrada")]
    [InlineData("formulario.schema.json", "properties/modeloOrigemCodigo")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/cursoNome")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/cursoGrau")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/unidadeOfertanteNome")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/turnos")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/quadro")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/totalDeVagasDoEdital")]
    [InlineData("distribuicao-de-vagas.schema.json", "$defs/modalidade/properties/descricao")]
    public void Campo_EReadOnlyComDescricao(string arquivo, string caminho)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(caminho);
        JsonNode campo = Navegar(arquivo, caminho);

        campo["readOnly"]!.GetValue<bool>().Should().BeTrue();
        campo["description"]!.GetValue<string>().Should().Contain("ignorado na importação");
    }

    [Theory(DisplayName = "O código de referência de cada descritivo não é readOnly")]
    [InlineData("identificacao.schema.json", "properties/localidadeCodigoIbge")]
    [InlineData("identificacao.schema.json", "properties/tipoProcessoCodigo")]
    [InlineData("identificacao.schema.json", "properties/unidadeAdministradoraCodigo")]
    [InlineData("cronograma.schema.json", "$defs/fase/properties/unidadeResponsavelCodigo")]
    [InlineData("exigencias.schema.json", "$defs/exigencia/properties/tipoDocumentoCodigo")]
    [InlineData("atendimento.schema.json", "$defs/condicao/properties/condicaoCodigo")]
    [InlineData("bonus-regional.schema.json", "properties/baseLegalBonusRegionalCodigo")]
    [InlineData("motivo.schema.json", "properties/codigo")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/ofertaCursoCodigo")]
    [InlineData("distribuicao-de-vagas.schema.json", "$defs/modalidade/properties/codigo")]
    [InlineData("motivo.schema.json", "properties/tipoCodigo")]
    public void Codigo_NaoEReadOnly(string arquivo, string caminho)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(caminho);

        Navegar(arquivo, caminho)["readOnly"].Should().BeNull();
    }

    [Fact(DisplayName = "O esquema não tem campo interno nos itens nem na raiz: só o id e a versão do processo, de leitura")]
    public void Esquema_NaoTemCampoInterno()
    {
        string[] internos = ["id", "criadoEm", "atualizadoEm", "criadoPor", "atualizadoPor", "createdBy", "updatedBy"];

        string[] nomes = [.. ArquivosDoEsquema.SelectMany(static arquivo => NomesDePropriedades(Esquema(arquivo), ehRaiz: arquivo == ArquivoDoDocumento))];

        nomes.Intersect(internos, StringComparer.Ordinal).Should().BeEmpty();
        Esquema(ArquivoDoDocumento)["properties"]!.AsObject().Select(static par => par.Key)
            .Intersect(internos.Where(static nome => nome != "id"), StringComparer.Ordinal).Should().BeEmpty();
    }

    // O id só é aceito na raiz do documento, onde identifica o processo; em qualquer item a identidade é a chave.
    private static IEnumerable<string> NomesDePropriedades(JsonNode? no, bool ehRaiz = false) => no switch
    {
        JsonObject objeto => objeto.SelectMany(par => NomesDoFilho(par, ehRaiz)),
        JsonArray lista => lista.SelectMany(item => NomesDePropriedades(item)),
        _ => [],
    };

    private static IEnumerable<string> NomesDoFilho(KeyValuePair<string, JsonNode?> par, bool ehRaiz) =>
        par.Key == "properties" && par.Value is JsonObject propriedades
            ? propriedades.SelectMany(p => (ehRaiz ? [] : new[] { p.Key }).Concat(NomesDePropriedades(p.Value)))
            : NomesDePropriedades(par.Value);

    private static JsonNode Navegar(string arquivo, string caminho) =>
        caminho.Split('/').Aggregate(Esquema(arquivo), static (no, passo) => no[passo]!);
}
