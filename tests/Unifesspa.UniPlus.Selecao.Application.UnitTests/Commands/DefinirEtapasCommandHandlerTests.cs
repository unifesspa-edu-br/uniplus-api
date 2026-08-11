namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirEtapasCommandHandlerTests
{
    private static readonly Guid TipoProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    private static readonly Guid TipoRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");

    /// <summary>Resolve os dois tipos usados nos testes deste arquivo — Prova Objetiva e Redação.</summary>
    private static ITipoEtapaReader ReaderPadrao()
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null));
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "REDACAO", "Redação", null));
        return reader;
    }

    private static TipoEtapaSnapshot TipoEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva").Value!;

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            Guid.CreateVersion7(),
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com etapas válidas persiste e retorna sucesso (CA-02)")]
    public async Task Handle_EtapasValidas_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva");
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        // O agregado é tracked (ObterParaMutacaoAsync); a persistência é por
        // change detection no SaveChanges — NÃO se chama DbSet.Update (que
        // marcaria os filhos novos com Guid v7 como Modified → UPDATE inválido).
        repository.DidNotReceive().Atualizar(Arg.Any<Domain.Entities.ProcessoSeletivo>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Id de etapa existente atualiza a MESMA instância (preserva etapa_ref)")]
    public async Task Handle_ComIdExistente_AtualizaMesmaInstancia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, 5m, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaNova = processo.Etapas.Single();
        etapaNova.Id.Should().NotBe(etapaOriginal.Id);
        etapaNova.Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "Handle com o mesmo Id de etapa repetido no payload é recusado")]
    public async Task Handle_ComIdEtapaRepetido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id),
                new EtapaProcessoInput("Prova Objetiva (duplicada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 2, etapaOriginal.Id),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com ordem de etapa duplicada não persiste (invariante do agregado)")]
    public async Task Handle_OrdemDuplicada_NaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 2m, null, 1),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "issue #1071 — CA-03: tipo de etapa inexistente ou inativo é recusado")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Guid tipoInexistente = Guid.CreateVersion7();
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, tipoInexistente, 3m, null, 1)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        processo.Etapas.Should().BeEmpty("nenhuma etapa pode ser persistida sem snapshot de tipo (CA-04)");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
