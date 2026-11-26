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
    [InlineData(ArquivoDoDocumento, "properties/tiposDeAto")]
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
    [InlineData("motivo.schema.json", "properties/sentidoRotulo")]
    [InlineData("prova-presencial.schema.json", "$defs/municipio/properties/nome")]
    [InlineData("motivo.schema.json", "properties/versaoDoEditalDeEntrada")]
    [InlineData("formulario.schema.json", "properties/modeloOrigemCodigo")]
    [InlineData("formulario.schema.json", "properties/modeloOrigemNome")]
    [InlineData("formulario.schema.json", "properties/consequenciaDaFaltaDeEnvioRotulo")]
    [InlineData("cronograma.schema.json", "$defs/fase/properties/tipoFaseNome")]
    [InlineData("comuns.schema.json", "$defs/publicacaoPrevista/properties/atoNome")]
    [InlineData("bancas.schema.json", "properties/categoriasDocumentoNomes")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/cursoNome")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/cursoGrau")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/unidadeOfertanteNome")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/turnos")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/quadro")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/totalDeVagasDoEdital")]
    [InlineData("distribuicao-de-vagas.schema.json", "$defs/modalidade/properties/descricao")]
    [InlineData("exigencias.schema.json", "$defs/exigencia/properties/finalidadeRotulo")]
    [InlineData("exigencias.schema.json", "$defs/exigencia/properties/aplicabilidadeRotulo")]
    [InlineData("exigencias.schema.json", "$defs/exigencia/properties/modelo/properties/formatoRotulo")]
    [InlineData("exigencias.schema.json", "$defs/formatosPermitidos/properties/lista/items/properties/formatoRotulo")]
    [InlineData("exigencias.schema.json", "$defs/prazoDeEmissao/properties/escolhaRotulo")]
    [InlineData("exigencias.schema.json", "$defs/prazoDeEmissao/properties/unidadeRotulo")]
    [InlineData("exigencias.schema.json", "$defs/prazoDeEmissao/properties/efeitoForaDoPrazoRotulo")]
    [InlineData("exigencias.schema.json", "$defs/validade/properties/escolhaRotulo")]
    [InlineData("exigencias.schema.json", "$defs/validade/properties/efeitoDoVencimentoRotulo")]
    [InlineData("exigencias.schema.json", "$defs/dataDeReferenciaDoDocumento/properties/tipoRotulo")]
    [InlineData("condicoes.schema.json", "$defs/baseLegal/properties/abrangenciaRotulo")]
    [InlineData("condicoes.schema.json", "$defs/baseLegal/properties/situacaoRotulo")]
    [InlineData("condicoes.schema.json", "$defs/condicao/properties/operadorRotulo")]
    [InlineData("condicoes.schema.json", "$defs/obrigatoriedade/properties/tipoRotulo")]
    [InlineData("regras-de-comprovacao.schema.json", "properties/formaRotulo")]
    [InlineData("regras-de-comprovacao.schema.json", "properties/criterioDeDistincaoRotulo")]
    [InlineData("comuns.schema.json", "$defs/propriedadesDoRecurso/properties/prazoUnidadeRotulo")]
    [InlineData("comuns.schema.json", "$defs/propriedadesDoRecurso/properties/prazoSegundaInstanciaUnidadeRotulo")]
    [InlineData("comuns.schema.json", "$defs/propriedadesDoRecurso/properties/efeitoSuspensivoRotulo")]
    [InlineData("comuns.schema.json", "$defs/propriedadesDoRecurso/properties/guardaDaVagaRotulo")]
    [InlineData("comuns.schema.json", "$defs/publicacaoPrevista/properties/papelRotulo")]
    [InlineData("comuns.schema.json", "$defs/procedimentoDeRecurso/properties/tipoDeDecisaoRotulo")]
    [InlineData("etapas.schema.json", "properties/tipoEtapaNome")]
    [InlineData("etapas.schema.json", "properties/caraterRotulo")]
    [InlineData("etapas.schema.json", "properties/formaDeAvaliacaoRotulo")]
    [InlineData("etapas.schema.json", "properties/tiposDeDecisaoRotulos")]
    [InlineData("etapas.schema.json", "$defs/recursoDaEtapa/properties/eventoInicialDoPrazoRotulo")]
    [InlineData("bancas.schema.json", "properties/tipoBancaNome")]
    [InlineData("bancas.schema.json", "$defs/regraDeDeliberacao/properties/modoRotulo")]
    [InlineData("bancas.schema.json", "$defs/regraDeDeliberacao/properties/criterioRotulo")]
    [InlineData("bancas.schema.json", "$defs/regraDeDeliberacao/properties/desempateRotulo")]
    [InlineData("bancas.schema.json", "$defs/regraDeDeliberacao/properties/encerramentoRotulo")]
    [InlineData("bancas.schema.json", "$defs/regraDeDeliberacao/properties/semUnanimidadeRotulo")]
    [InlineData("cronograma.schema.json", "$defs/fase/properties/tiposDeDecisaoRotulos")]
    [InlineData("homologacao.schema.json", "$defs/condicao/properties/tipoRotulo")]
    [InlineData("restricao-de-valor.schema.json", "properties/tipoRotulo")]
    [InlineData("formulario.schema.json", "properties/finalidadeRotulo")]
    [InlineData("formulario.schema.json", "$defs/pagina/properties/tipoRotulo")]
    [InlineData("formulario.schema.json", "$defs/pagina/properties/blocoRotulo")]
    [InlineData("identificacao.schema.json", "properties/origemCandidatosRotulo")]
    [InlineData("motivo.schema.json", "properties/tipoDeDecisaoCodigoRotulo")]
    [InlineData("coleta.schema.json", "properties/referenciaTemporalDosDados/properties/tipoRotulo")]
    [InlineData("taxa-de-inscricao.schema.json", "properties/fundamentosRotulos")]
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
    [InlineData("fada.schema.json", "properties/baseLegalCodigo")]
    [InlineData("motivo.schema.json", "properties/codigo")]
    [InlineData("distribuicao-de-vagas.schema.json", "properties/ofertaCursoCodigo")]
    [InlineData("distribuicao-de-vagas.schema.json", "$defs/modalidade/properties/codigo")]
    [InlineData("motivo.schema.json", "properties/tipoDeDecisaoCodigo")]
    public void Codigo_NaoEReadOnly(string arquivo, string caminho)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(caminho);

        Navegar(arquivo, caminho)["readOnly"].Should().BeNull();
    }

    private static JsonNode Navegar(string arquivo, string caminho) =>
        caminho.Split('/').Aggregate(Esquema(arquivo), static (no, passo) => no[passo]!);
}
