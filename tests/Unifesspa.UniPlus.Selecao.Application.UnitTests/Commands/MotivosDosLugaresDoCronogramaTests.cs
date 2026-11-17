namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A fase que sai do cronograma leva junto os motivos pendentes dela, e o motivo já publicado recusa a substituição
/// (UNI-REQ-0124).
/// </summary>
public sealed class MotivosDosLugaresDoCronogramaTests
{
    [Fact(DisplayName = "No rascunho, a fase que sai do cronograma leva os motivos escolhidos nela")]
    public async Task Rascunho_FaseQueSai_LevaOsMotivos()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput queFica = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());
        FaseCronogramaInput queSai = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with { Ordem = 2 };
        (await cenario.DefinirFasesAsync(queFica, queSai)).IsSuccess.Should().BeTrue();
        MotivoDoProcesso naFaseQueSai = Escolhido(cenario.Processo, LugarDaDecisao.DaFase(queSai.FaseCanonicaId), Motivo());
        cenario.MotivosEscolhidos.ListarEscolhaAtualAsync(cenario.Processo.Id, Arg.Any<CancellationToken>()).Returns([naFaseQueSai]);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(queFica);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.MotivosEscolhidos.Received(1).Remover(naFaseQueSai);
    }

    [Fact(DisplayName = "Na retificação, a fase que sai com motivo já publicado recusa a substituição, descarta o rastreamento e não grava")]
    public async Task Retificacao_FaseComMotivoPublicado_Recusa()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput queFica) = RetificacaoComMotivoPublicadoNaFaseQueSai();

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(new DefinirCronogramaFasesCommand(cenario.Processo.Id, [queFica], PrecondicaoIfMatch.Curinga));

        resultado.Errors.Should().Contain(e => e.Error.Code == MotivoDeDecisaoErrorCodes.FaseOuEtapaComMotivoPublicado);
        cenario.MotivosEscolhidos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    private static (CenarioDoCronogramaDeFases Cenario, FaseCronogramaInput QueFica) RetificacaoComMotivoPublicadoNaFaseQueSai()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        FaseCronograma conforme = processo.CronogramaFases.First();
        FaseCronograma extra = FaseExtra(conforme);
        processo.DefinirCronogramaFases([conforme, extra], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        MotivoDoProcesso publicado = Escolhido(processo, LugarDaDecisao.DaFase(extra.FaseCanonicaOrigemId), Motivo());
        AbrirRetificacao(processo, Publicar(processo));
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.Para(processo).ComTipoDeAto(TipoAtoDaClassificacao()).ComFasesIniciadas(FasesIniciadas.Nenhuma);
        cenario.MotivosEscolhidos.ListarEscolhaAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([publicado]);

        return (cenario, cenario.CadastrarFase(FaseCanonicaDoResultadoFinal() with { Id = conforme.FaseCanonicaOrigemId, Codigo = conforme.Codigo }));
    }

    private static FaseCronograma FaseExtra(FaseCronograma conforme) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), "RESULTADO_PRELIMINAR", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(conforme.Ordem + 1, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
}
