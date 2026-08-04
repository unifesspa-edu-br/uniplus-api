namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CriarProcessoSeletivoCommandHandlerTests
{
    private static readonly Guid UnidadeId = Guid.NewGuid();

    private static readonly UnidadeView UnidadeCeps = new(
        UnidadeId, "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null);

    [Fact(DisplayName = "Handle persiste o processo em rascunho com a Unidade resolvida e retorna o id (CA-01, CA-03)")]
    public async Task Handle_PersisteComUnidadeResolvidaERetornaId()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        CriarProcessoSeletivoCommand command = new("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId);
        ProcessoSeletivo? processoPersistido = null;
        repository.When(r => r.AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>()))
            .Do(ci => processoPersistido = ci.Arg<ProcessoSeletivo>());

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processoPersistido.Should().NotBeNull();
        result.Value.Should().Be(processoPersistido!.Id, "o id devolvido tem de ser o do processo efetivamente persistido");
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ProcessoSeletivo>(p =>
                p.Nome == "PS 2026 — SiSU"
                && p.Status == StatusProcesso.Rascunho
                && p.UnidadeAdministradoraOrigemId == UnidadeId
                && p.UnidadeAdministradora.Sigla == "CEPS"
                && p.UnidadeAdministradora.Slug == "ceps"
                && p.UnidadeAdministradora.Nome == "Centro de Processos Seletivos"
                && p.UnidadeAdministradora.Tipo == "ADMINISTRATIVA"),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa quando a Unidade administradora não é encontrada e não persiste nada (CA-02)")]
    public async Task Handle_UnidadeNaoEncontrada_RecusaSemPersistir()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        IUnidadeReader unidadeReader = Substitute.For<IUnidadeReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        unidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        CriarProcessoSeletivoCommand command = new("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId);

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unidadeReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
