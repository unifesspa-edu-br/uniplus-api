namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

public sealed class RetiradaDeMotivoPublicadoTests
{
    [Fact(DisplayName = "Retirar motivo publicado é recusado sem removê-lo nem gravar")]
    public async Task Retirar_MotivoPublicado_NaoRemoveNemGrava()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        MotivoDoProcesso motivo = Escolhido(processo, LugarDaPrimeiraFase(processo), Motivo());
        Publicar(processo);
        IMotivoDoProcessoRepository motivos = Substitute.For<IMotivoDoProcessoRepository>();
        motivos.ListarEscolhaAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([motivo]);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resposta = await RetirarMotivoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDoProcessoCommand(processo.Id, motivo.Lugar, motivo.MotivoDeDecisaoOrigemId),
            Repositorio(processo), motivos, unitOfWork, CancellationToken.None);

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        motivos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static IProcessoSeletivoRepository Repositorio(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return processos;
    }
}
