namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirIdentificadorLegivelCommandHandlerTests
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

    [Fact(DisplayName = "Handle declara o identificador e persiste")]
    public async Task Handle_Declara_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, "psiq-2026", PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.IdentificadorLegivel!.Value.Valor.Should().Be("psiq-2026");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Formato inválido é recusado sem carregar o processo")]
    public async Task Handle_FormatoInvalido_RecusaSemIO()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(Guid.CreateVersion7(), "psiq 2026", PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido);
        await mocks.Repository.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "Identificador em uso por outro processo é recusado e o rastreamento é descartado")]
    public async Task Handle_EmUso_RecusaSemAlterar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.Repository.IdentificadorLegivelEmUsoAsync(
                IdentificadorLegivel.Criar("psiq-2026").Value, processo.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, "psiq-2026", PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso);
        mocks.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Processo publicado recusa antes de consultar a unicidade")]
    public async Task Handle_Publicado_RecusaAntesDaUnicidade()
    {
        ProcessoSeletivo processo = NovoProcesso();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, "psiq-2026", PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await mocks.Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
    }

    [Fact(DisplayName = "Identificador ausente remove a declaração sem consultar a unicidade")]
    public async Task Handle_Ausente_Remove()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirIdentificadorLegivel(IdentificadorLegivel.Criar("psiq-2026").Value, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, null, PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.IdentificadorLegivel.Should().BeNull();
        await mocks.Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
    }
}
