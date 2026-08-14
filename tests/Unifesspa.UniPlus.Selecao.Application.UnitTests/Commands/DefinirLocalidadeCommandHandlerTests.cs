namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirLocalidadeCommandHandlerTests
{
    private sealed record Mocks(IProcessoSeletivoRepository Repository, ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<ISelecaoUnitOfWork>());
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirLocalidadeCommand command = new(Guid.CreateVersion7(), "1501402", "Belém", "PA", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirLocalidadeCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle troca a localidade declarada e persiste")]
    public async Task Handle_TrocaLocalidade_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirLocalidadeCommand command = new(processo.Id, "1501402", "Belém", "PA", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirLocalidadeCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Localidade.CodigoIbge.Should().Be("1501402");
        processo.Localidade.Nome.Should().Be("Belém");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Redeclarar o mesmo município com o nome corrigido tem de gravar o nome novo. Como a
    /// igualdade de <see cref="LocalidadeRegente"/> olha só o código, uma comparação antes
    /// de atribuir descartaria a correção silenciosamente — e o rótulo errado ficaria.
    /// </summary>
    [Fact(DisplayName = "Redeclarar o mesmo código com o nome corrigido grava o nome novo")]
    public async Task Handle_MesmoCodigoNomeCorrigido_GravaNomeNovo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirLocalidade(
            LocalidadeRegente.Criar("1504208", "Maraba sem acento", "PA").Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirLocalidadeCommand command = new(processo.Id, "1504208", "Marabá", "PA", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirLocalidadeCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Localidade.Nome.Should().Be("Marabá");
    }

    [Fact(DisplayName = "Trio inteiramente ausente é recusado com o erro nomeado do catálogo")]
    public async Task Handle_TrioAusente_RecusaComErroNomeado()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirLocalidadeCommand command = new(processo.Id, null, null, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirLocalidadeCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");
        processo.Localidade.CodigoIbge.Should().Be("1504208", "a localidade anterior permanece");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Trio incoerente é recusado pela causa que o Kernel nomeia")]
    public async Task Handle_TrioIncoerente_RecusaPelaCausaDoKernel()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirLocalidadeCommand command = new(processo.Id, "1504208", "Marabá", "SP", PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirLocalidadeCommandHandler.Handle(
            command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CidadeReferencia.UfIncoerente");
    }
}
