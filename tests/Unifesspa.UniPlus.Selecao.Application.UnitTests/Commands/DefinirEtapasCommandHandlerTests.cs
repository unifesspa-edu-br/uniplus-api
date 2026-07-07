namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class DefinirEtapasCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            Guid.CreateVersion7(),
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, 3m, null, 1)]);

        Result result = await DefinirEtapasCommandHandler.Handle(command, repository, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com etapas válidas persiste e retorna sucesso (CA-02)")]
    public async Task Handle_EtapasValidas_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, 3m, null, 1)]);

        Result result = await DefinirEtapasCommandHandler.Handle(command, repository, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva");
        // O agregado é tracked (ObterComConfiguracaoAsync); a persistência é por
        // change detection no SaveChanges — NÃO se chama DbSet.Update (que
        // marcaria os filhos novos com Guid v7 como Modified → UPDATE inválido).
        repository.DidNotReceive().Atualizar(Arg.Any<Domain.Entities.ProcessoSeletivo>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Id de etapa existente atualiza a MESMA instância (preserva etapa_ref)")]
    public async Task Handle_ComIdExistente_AtualizaMesmaInstancia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal]);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, 3m, 5m, 1, etapaOriginal.Id)]);

        Result result = await DefinirEtapasCommandHandler.Handle(command, repository, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaAtualizada = processo.Etapas.Single();
        etapaAtualizada.Id.Should().Be(etapaOriginal.Id);
        etapaAtualizada.Should().BeSameAs(etapaOriginal);
        etapaAtualizada.Nome.Should().Be("Prova Objetiva (revisada)");
        etapaAtualizada.Peso.Should().Be(3m);
    }

    [Fact(DisplayName = "Handle sem Id (ou com Id sem correspondência) cria etapa nova")]
    public async Task Handle_SemId_CriaEtapaNova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal]);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, 2m, null, 1)]);

        Result result = await DefinirEtapasCommandHandler.Handle(command, repository, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaNova = processo.Etapas.Single();
        etapaNova.Id.Should().NotBe(etapaOriginal.Id);
        etapaNova.Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "Handle com o mesmo Id de etapa repetido no payload é recusado")]
    public async Task Handle_ComIdEtapaRepetido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal]);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, 1m, null, 1, etapaOriginal.Id),
                new EtapaProcessoInput("Prova Objetiva (duplicada)", CaraterEtapa.Classificatoria, 2m, null, 2, etapaOriginal.Id),
            ]);

        Result result = await DefinirEtapasCommandHandler.Handle(command, repository, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com ordem de etapa duplicada não persiste (invariante do agregado)")]
    public async Task Handle_OrdemDuplicada_NaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, 3m, null, 1),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, 2m, null, 1),
            ]);

        Result result = await DefinirEtapasCommandHandler.Handle(command, repository, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
