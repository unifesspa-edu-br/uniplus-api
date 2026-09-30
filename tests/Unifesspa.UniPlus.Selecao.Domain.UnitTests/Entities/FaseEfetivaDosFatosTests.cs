namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A fase em que um fato citado por gatilho de exigência fica conhecido no processo (UNI-REQ-0144,
/// UNI-REQ-0077): a mais tardia entre a fase do catálogo e a do formulário que o produz, e a do
/// derivado não antes das suas dependências; fato só da isenção fica na fase da isenção.
/// </summary>
public sealed class FaseEfetivaDosFatosTests
{
    private static readonly Dictionary<string, string> TudoNaInscricao = new(StringComparer.Ordinal)
    {
        ["TEM_RENDA"] = "INSCRICAO",
        ["COMPROVANTE_RENDA"] = "INSCRICAO",
        ["BOLSISTA"] = "INSCRICAO",
    };

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false, bool coletaIsencao = false) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao,
            inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static FatoColetado Item(string codigo) =>
        FatoColetado.Criar(codigo, 0, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao).Value!;

    private static void DefinirDerivado(ProcessoSeletivo processo, string derivado, string dependencia) =>
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar(derivado,
                [RegraDerivacaoConfigurada.Criar(0, "SIM", [CondicaoRegraDerivacao.Criar(0, dependencia, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
                .Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static (ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Isencao, FaseCronograma Habilitacao) Processo()
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        FaseCronograma isencao = Fase(2, "SOLICITACAO_ISENCAO", coletaIsencao: true);
        FaseCronograma habilitacao = Fase(3, FormularioProcesso.CodigoFaseHabilitacao);
        Result cronograma = processo.DefinirCronogramaFases([inscricao, isencao, habilitacao], [], PrecondicaoIfMatch.Ausente);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
        foreach ((FinalidadeFormulario finalidade, FaseCronograma fase, string fato) in new[]
        {
            (FinalidadeFormulario.Inscricao, inscricao, "TEM_RENDA"),
            (FinalidadeFormulario.IsencaoTaxa, isencao, "BOLSISTA"),
            (FinalidadeFormulario.Habilitacao, habilitacao, "COMPROVANTE_RENDA"),
        })
        {
            processo.DefinirFormulario(finalidade, fase.Id, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
            processo.DefinirFatosColetados(finalidade, [Item(fato)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        return (processo, inscricao, isencao, habilitacao);
    }

    [Fact(DisplayName = "Fato coletado só na habilitação não é citado por exigência da inscrição, mesmo que o catálogo o dê na inscrição")]
    public void FatoDaHabilitacao_EmExigenciaDaInscricao_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho("COMPROVANTE_RENDA", inscricao.Id, TudoNaInscricao)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        processo.RecusaDeFaseDoGatilho("COMPROVANTE_RENDA", habilitacao.Id, TudoNaInscricao).Should().BeNull();
    }

    [Fact(DisplayName = "O derivado fica conhecido na fase da sua dependência mais tardia")]
    public void Derivado_HerdaAFaseDaDependencia()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _, _) = Processo();
        DefinirDerivado(processo, "RENDA_COMPROVADA", "COMPROVANTE_RENDA");

        processo.RecusaDeFaseDoGatilho("RENDA_COMPROVADA", inscricao.Id, TudoNaInscricao)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
    }

    [Fact(DisplayName = "Fato só da isenção é citado só na fase da isenção, nem mesmo na habilitação, que vem depois")]
    public void FatoDaIsencao_SoNaFaseDaIsencao()
    {
        (ProcessoSeletivo processo, _, FaseCronograma isencao, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho("BOLSISTA", habilitacao.Id, TudoNaInscricao)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoForaDaFaseDeIsencao);
        processo.RecusaDeFaseDoGatilho("BOLSISTA", isencao.Id, TudoNaInscricao).Should().BeNull();
    }

    [Fact(DisplayName = "O derivado de fato da isenção também só é citado na fase da isenção")]
    public void DerivadoDeFatoDaIsencao_ForaDaFaseDaIsencao_Recusa()
    {
        (ProcessoSeletivo processo, _, _, FaseCronograma habilitacao) = Processo();
        DefinirDerivado(processo, "ISENTO_POR_BOLSA", "BOLSISTA");

        processo.RecusaDeFaseDoGatilho("ISENTO_POR_BOLSA", habilitacao.Id, TudoNaInscricao)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoForaDaFaseDeIsencao);
    }

    [Fact(DisplayName = "Fato que o catálogo situa em fase fora do cronograma é recusado")]
    public void PontoDoCatalogoForaDoCronograma_Recusa()
    {
        (ProcessoSeletivo processo, _, _, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho(
                "MODALIDADE_CONVOCACAO", habilitacao.Id, new Dictionary<string, string>(StringComparer.Ordinal) { ["MODALIDADE_CONVOCACAO"] = "RESULTADO_FINAL" })!
            .Code.Should().Be(DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma);
    }
}
