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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): a primeira etapa do payload (vínculo
    /// inalterado) já mutou a instância tracked via AtualizarDados quando a SEGUNDA falha por
    /// tipo inativo — sem descartar o rastreamento, o SaveChangesAsync automático do Wolverine
    /// persistiria essa mutação parcial mesmo com o PUT inteiro recusado.
    /// </summary>
    [Fact(DisplayName = "Handle com etapa anterior mutada e etapa posterior com tipo inativo descarta o rastreamento antes de recusar")]
    public async Task Handle_ComEtapaAnteriorMutadaETipoInativoNaPosterior_DescartaRastreamento()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        Guid tipoInativo = Guid.CreateVersion7();
        tipoEtapaReader.ObterAtivoPorIdAsync(tipoInativo, Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                // Vínculo inalterado — AtualizarDados roda e muta etapaOriginal (tracked).
                new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id),
                // Etapa nova com tipo inativo — falha DEPOIS que a etapa acima já mutou.
                new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, tipoInativo, 1m, null, 2),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): desativar um tipo já vinculado a uma etapa
    /// não pode bloquear PUTs subsequentes da coleção inteira — só vínculos NOVOS ou que MUDAM
    /// de tipo precisam do cadastro ativo. O snapshot já congelado é reaproveitado sem nova
    /// leitura do cadastro.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado reaproveita o snapshot já congelado, mesmo com o tipo desde então desativado")]
    public async Task Handle_ComVinculoInalterado_ReaproveitaSnapshotMesmoComTipoDesativado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        // Reader configurado para devolver null para TODO Id — simula o tipo desativado depois
        // que a etapa já existia. Se o handler tentasse reavaliar o vínculo inalterado, a
        // publicação inteira recusaria com TipoEtapaNaoEncontradoOuInativo.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // Edita só o Peso — TipoEtapaOrigemId permanece o mesmo do snapshot já congelado.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(9m);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): o snapshot-copy (ADR-0061) só muda quando o
    /// VÍNCULO muda — renomear o tipo no cadastro, ainda ativo, não pode reescrever
    /// silenciosamente o snapshot de uma etapa que não trocou de TipoEtapaOrigemId.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado não atualiza o Nome congelado, mesmo com o tipo renomeado no cadastro")]
    public async Task Handle_ComVinculoInalterado_NaoAtualizaNomeAoRenomearTipoNoCadastro()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        // Se o handler fosse reler o cadastro para um vínculo inalterado, encontraria este Nome
        // renomeado — a asserção abaixo prova que ele nunca chega a consultar.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)", null));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Nome.Should().Be("Prova Objetiva",
            "o snapshot só muda quando o VÍNCULO (TipoEtapaOrigemId) muda, não quando o cadastro de origem muda");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Handle com vínculo alterado resolve o novo tipo contra o cadastro corrente")]
    public async Task Handle_ComVinculoAlterado_ResolveContraCadastroAtual()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // Mesma etapa (mesmo Id), agora vinculada à Redação em vez de Prova Objetiva.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1, etapaOriginal.Id)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("REDACAO");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// issue #1108 (achado de review do PR #1071): TipoEtapa é owned type (OwnsOne) de
    /// EtapaProcesso — EF Core não aceita a MESMA instância de TipoEtapaSnapshot pertencendo a
    /// duas etapas ao mesmo tempo. Duas etapas novas com o mesmo tipo é caso legítimo (duas
    /// fases classificatórias do mesmo propósito institucional); o cache de resolução tem de
    /// reaproveitar só o dado do catálogo, nunca a instância do snapshot já construído.
    /// </summary>
    [Fact(DisplayName = "Handle com duas etapas novas do mesmo tipo cria instâncias distintas de TipoEtapaSnapshot")]
    public async Task Handle_ComDuasEtapasMesmoTipo_CriaInstanciasDistintas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova — Manhã", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
                new EtapaProcessoInput("Prova — Tarde", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirEtapasCommandHandler.Handle(command, repository, tipoEtapaReader, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        EtapaProcesso[] etapas = [.. processo.Etapas.OrderBy(e => e.Ordem)];
        etapas.Should().HaveCount(2);
        etapas[0].TipoEtapa.Should().NotBeSameAs(etapas[1].TipoEtapa,
            "TipoEtapa é owned type — a mesma instância em dois donos quebraria o change tracking do EF Core");
        etapas[0].TipoEtapa.Should().Be(etapas[1].TipoEtapa, "mesmo conteúdo, apesar de instâncias diferentes (record com igualdade por valor)");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "issue #1071 — CA-03: tipo de etapa inexistente ou inativo é recusado")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
