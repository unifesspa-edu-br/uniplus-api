namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os pares valor e código dos enums da configuração do processo, escritos aqui de propósito: alterar um código é alterar o
/// contrato publicado e o dado gravado, e este teste é o que obriga a revisão. O rótulo é texto de tela e não é fixado.
/// </summary>
public sealed class CodigosDeContratoDaConfiguracaoDoProcessoTests
{
    [Fact(DisplayName = "Cada caráter da etapa tem o código que o contrato publica")]
    public void CaraterEtapa_CodigoDeCadaValor() =>
        CodigosDe<CaraterEtapa>().Should().Equal(new Dictionary<CaraterEtapa, string>
        {
            [CaraterEtapa.Classificatoria] = "CLASSIFICATORIA",
            [CaraterEtapa.Eliminatoria] = "ELIMINATORIA",
            [CaraterEtapa.Ambas] = "AMBAS",
        });

    [Fact(DisplayName = "Cada forma de avaliação tem o código que o contrato publica")]
    public void FormaDeAvaliacao_CodigoDeCadaValor() =>
        CodigosDe<FormaDeAvaliacao>().Should().Equal(new Dictionary<FormaDeAvaliacao, string>
        {
            [FormaDeAvaliacao.Objetiva] = "OBJETIVA",
            [FormaDeAvaliacao.Humana] = "HUMANA",
        });

    [Fact(DisplayName = "Cada evento que inicia o prazo tem o código que o contrato publica")]
    public void EventoInicialDoPrazo_CodigoDeCadaValor() =>
        CodigosDe<EventoInicialDoPrazo>().Should().Equal(new Dictionary<EventoInicialDoPrazo, string>
        {
            [EventoInicialDoPrazo.PublicacaoDoAto] = "PUBLICACAO_DO_ATO",
            [EventoInicialDoPrazo.CienciaIndividual] = "CIENCIA_INDIVIDUAL",
        });

    [Fact(DisplayName = "Cada papel da publicação prevista tem o código que o contrato publica")]
    public void PapelDaPublicacaoPrevista_CodigoDeCadaValor() =>
        CodigosDe<PapelDaPublicacaoPrevista>().Should().Equal(new Dictionary<PapelDaPublicacaoPrevista, string>
        {
            [PapelDaPublicacaoPrevista.Preliminar] = "PRELIMINAR",
            [PapelDaPublicacaoPrevista.Definitivo] = "DEFINITIVO",
        });

    [Fact(DisplayName = "Cada aplicabilidade da exigência tem o código que o contrato publica")]
    public void Aplicabilidade_CodigoDeCadaValor() =>
        CodigosDe<Aplicabilidade>().Should().Equal(new Dictionary<Aplicabilidade, string>
        {
            [Aplicabilidade.Geral] = "GERAL",
            [Aplicabilidade.Condicional] = "CONDICIONAL",
        });

    [Fact(DisplayName = "Cada categoria de obrigatoriedade tem o código que o contrato publica")]
    public void CategoriaObrigatoriedade_CodigoDeCadaValor() =>
        CodigosDe<CategoriaObrigatoriedade>().Should().Equal(new Dictionary<CategoriaObrigatoriedade, string>
        {
            [CategoriaObrigatoriedade.Etapa] = "ETAPA",
            [CategoriaObrigatoriedade.Modalidade] = "MODALIDADE",
            [CategoriaObrigatoriedade.Desempate] = "DESEMPATE",
            [CategoriaObrigatoriedade.Documento] = "DOCUMENTO",
            [CategoriaObrigatoriedade.Fada] = "FADA",
            [CategoriaObrigatoriedade.Atendimento] = "ATENDIMENTO",
            [CategoriaObrigatoriedade.Outros] = "OUTROS",
        });

    [Fact(DisplayName = "Cada origem dos candidatos tem o código que o contrato publica")]
    public void OrigemCandidatos_CodigoDeCadaValor() =>
        CodigosDe<OrigemCandidatos>().Should().Equal(new Dictionary<OrigemCandidatos, string>
        {
            [OrigemCandidatos.InscricaoPropria] = "INSCRICAO_PROPRIA",
            [OrigemCandidatos.ImportacaoExterna] = "IMPORTACAO_EXTERNA",
        });

    [Fact(DisplayName = "Cada origem da data da fase tem o código que o contrato publica")]
    public void OrigemDataFase_CodigoDeCadaValor() =>
        CodigosDe<OrigemDataFase>().Should().Equal(new Dictionary<OrigemDataFase, string>
        {
            [OrigemDataFase.NoEdital] = "NO_EDITAL",
            [OrigemDataFase.NaExecucao] = "NA_EXECUCAO",
        });

    [Fact(DisplayName = "Cada origem das opções do dado coletado tem o código que o contrato publica")]
    public void OrigemValoresColeta_CodigoDeCadaValor() =>
        CodigosDe<OrigemValoresColeta>().Should().Equal(new Dictionary<OrigemValoresColeta, string>
        {
            [OrigemValoresColeta.Catalogo] = "CATALOGO",
            [OrigemValoresColeta.OpcoesDoProcesso] = "OPCOES_DO_PROCESSO",
            [OrigemValoresColeta.MunicipiosDoFada] = "MUNICIPIOS_DO_FADA",
            [OrigemValoresColeta.UnidadesFederativas] = "UNIDADES_FEDERATIVAS",
            [OrigemValoresColeta.MunicipiosDaProvaPresencial] = "MUNICIPIOS_DA_PROVA_PRESENCIAL",
        });

    private static Dictionary<TEnum, string> CodigosDe<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().ToDictionary(static valor => valor, static valor => valor.Codigo());
}
