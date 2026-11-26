namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoEnemComCalculoLocal;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>
/// A conferência que antecede o congelamento da publicação e da retificação recusa a confirmação do INEP de uma edição que o
/// processo não declara. O rascunho continua salvando, em qualquer ordem das edições e da regra.
/// </summary>
public sealed class EdicaoDoExameDaHomologacaoNaConferenciaTests
{
    private const string FaseDaRegra = "fases[0]";
    private const string CampoDaEdicao = FaseDaRegra + ".regraDeHomologacao.alternativas[0].condicoes[0].edicaoDoExame";

    private static ProcessoSeletivo ProcessoComAsEdicoes(params string[] edicoes)
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [.. edicoes.Select(static (edicao, ordem) => OpcaoDeclaradaFato.Criar("EDICAO_ENEM", edicao, edicao, ordem).Value!)])
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FieldError[] CausasDaEdicao(ProcessoSeletivo processo) =>
    [
        .. ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors
            .Where(static causa => causa.Error.Code == HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada),
    ];

    // A regra do processo de teste confirma o INEP da edição 2026.
    [Fact(DisplayName = "A edição da regra que o processo declara não impede congelar")]
    public void Conferir_EdicaoDaRegraDeclarada_NaoRecusa() =>
        CausasDaEdicao(ProcessoComAsEdicoes("2025", "2026")).Should().BeEmpty();

    [Fact(DisplayName = "A edição da regra que o processo não declara impede congelar, no campo da condição")]
    public void Conferir_EdicaoDaRegraNaoDeclarada_RecusaNoCampoDaCondicao()
    {
        FieldError causa = CausasDaEdicao(ProcessoComAsEdicoes("2024", "2025")).Should().ContainSingle().Subject;

        causa.Field.Should().Be(CampoDaEdicao);
    }

    [Fact(DisplayName = "O rascunho salva as edições declaradas mesmo quando a regra confirma o INEP de outra edição")]
    public void DefinirOpcoesDeclaradas_RegraComEdicaoNaoDeclarada_SalvaORascunho()
    {
        ProcessoSeletivo processo = ProcessoEnemLocal([OfertaAmpla(10, GrupoTecnologica)]);

        Result salvo = processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2024", "2024", 0).Value!]);

        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);
    }

    [Fact(DisplayName = "O rascunho salva a regra que confirma o INEP de uma edição que o processo ainda não declara")]
    public void DefinirCronogramaFases_EdicaoDaRegraNaoDeclarada_SalvaORascunho()
    {
        ProcessoSeletivo processo = ProcessoComAsEdicoes("2024");

        Result salvo = processo.DefinirCronogramaFases([FaseQueConfirmaOInepDe(2023)], []);

        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);
        CausasDaEdicao(processo).Should().ContainSingle("a recusa fica para a publicação e a retificação");
    }

    private static FaseCronograma FaseQueConfirmaOInepDe(int edicao) => FaseCronograma.Criar(
        ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologaComRecurso() with
        {
            RegraDeHomologacao = RegraDeHomologacaoDasInscricoes.Declarar(
                HomologacaoDasInscricoesDeTeste.ResultadoDaFase,
                [Alternativa(CondicaoDeHomologacao.Declarar(HomologacaoDasInscricoesDeTeste.CodigoDaConfirmacaoDoInep, edicao, HomologacaoDasInscricoesDeTeste.MotivoSemResultadoNoInep, enquadramento: null).Value!)]),
        },
        bancasRequeridas: HomologacaoDasInscricoesDeTeste.BancasDaFaseQueHomologaComRecurso()).Value!;
}
