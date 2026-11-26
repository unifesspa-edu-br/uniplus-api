namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaCascataDeRemanejamento;

public sealed class ProcessoSeletivoEmEdicaoTests
{
    [Fact(DisplayName = "ObterAsync com processo inexistente recusa com a resposta única de processo não encontrado")]
    public async Task ObterAsync_ProcessoInexistente_RecusaComNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repositorio = RepositorioCom(null, processoId);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(repositorio, processoId, CancellationToken.None);

        obtido.IsFailure.Should().BeTrue();
        obtido.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        obtido.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "ObterAsync com processo já publicado recusa pelo bloqueio de alteração depois da publicação")]
    public async Task ObterAsync_ProcessoPublicado_RecusaPeloBloqueio()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        IProcessoSeletivoRepository repositorio = RepositorioCom(processo, processo.Id);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(repositorio, processo.Id, CancellationToken.None);

        obtido.IsFailure.Should().BeTrue();
        obtido.Error.Should().Be(processo.MutacaoBloqueada());
    }

    [Fact(DisplayName = "ObterAsync com processo em rascunho devolve o próprio processo")]
    public async Task ObterAsync_ProcessoEmRascunho_DevolveOProcesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        IProcessoSeletivoRepository repositorio = RepositorioCom(processo, processo.Id);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(repositorio, processo.Id, CancellationToken.None);

        obtido.IsSuccess.Should().BeTrue();
        obtido.Value.Should().BeSameAs(processo);
    }

    private static IProcessoSeletivoRepository RepositorioCom(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }
}
