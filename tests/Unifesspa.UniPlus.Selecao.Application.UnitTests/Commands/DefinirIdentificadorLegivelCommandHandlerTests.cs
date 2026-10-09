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
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

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

    [Fact(DisplayName = "Formato inválido é recusado sem alterar a raiz nem consultar a unicidade")]
    public async Task Handle_FormatoInvalido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        IdentificadorLegivel? original = processo.IdentificadorLegivel;
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, "psiq 2026", PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido);
        processo.IdentificadorLegivel.Should().Be(original);
        await mocks.Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
    }

    [Fact(DisplayName = "Processo publicado sem sessão recusa pela precondição antes do formato")]
    public async Task Handle_PublicadoSemSessao_FormatoInvalido_RecusaPelaPrecondicao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, "psiq 2026", PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
            "o gate de estado e concorrência sai antes da validação do valor");
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

    [Theory(DisplayName = "Identificador ausente é recusado com erro nomeado, e o gravado permanece")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_Ausente_Recusa(string? identificador)
    {
        ProcessoSeletivo processo = NovoProcesso();
        IdentificadorLegivel? original = processo.IdentificadorLegivel;
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirIdentificadorLegivelCommandHandler.Handle(
            new DefinirIdentificadorLegivelCommand(processo.Id, identificador, PrecondicaoIfMatch.Ausente),
            mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
        processo.IdentificadorLegivel.Should().Be(original);
        await mocks.Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }
}
