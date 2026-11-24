namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirConfiguracaoDivulgacaoCommandHandlerTests
{
    private sealed record Mocks(IProcessoSeletivoRepository Repository, IUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<IUnitOfWork>());
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirConfiguracaoDivulgacaoCommand command = new(Guid.CreateVersion7(), null, null);

        Result result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com CamposPublicos nulo remove a configuração explícita e persiste (toggle por ausência)")]
    public async Task Handle_CamposPublicosNulo_RemoveConfiguracaoEPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME_ABREVIADO"], null).Value!).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(processo.Id, null, null);

        Result result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao.Should().BeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com campos válidos define a configuração e persiste")]
    public async Task Handle_CamposValidos_DefineConfiguracaoEPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(
            processo.Id, ["NUMERO_INSCRICAO", "NOME"], "Ampliação para dar transparência ao resultado.");

        Result result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao.Should().NotBeNull();
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("NOME", "NUMERO_INSCRICAO");
        processo.ConfiguracaoDivulgacao.Justificativa.Should().Be("Ampliação para dar transparência ao resultado.");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com campo fora do vocabulário propaga o erro do domínio e NÃO persiste")]
    public async Task Handle_CampoInvalido_PropagaErroDoDominioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(processo.Id, ["NUMERO_INSCRICAO", "cpf"], null);

        Result result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDivulgacao.CampoNaoPermitido");
        processo.ConfiguracaoDivulgacao.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle substitui integralmente uma configuração já existente")]
    public async Task Handle_SubstituiConfiguracaoExistente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME_ABREVIADO"], null).Value!).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(processo.Id, ["NUMERO_INSCRICAO"], null);

        Result result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("NUMERO_INSCRICAO");
    }

    [Fact(DisplayName = "Handle com processo já publicado propaga MutacaoPosPublicacaoBloqueada e NÃO persiste (CA-06)")]
    public async Task Handle_ProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirConfiguracaoDivulgacaoCommand command = new(
            processo.Id, ["NUMERO_INSCRICAO", "NOME_ABREVIADO"], null);

        Result result = await DefinirConfiguracaoDivulgacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.ConfiguracaoDivulgacao.Should().BeNull("a mutação bloqueada não pode ter tocado a configuração viva");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
