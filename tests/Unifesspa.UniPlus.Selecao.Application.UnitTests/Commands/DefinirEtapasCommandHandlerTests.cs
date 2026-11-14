namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            Guid.CreateVersion7(),
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com etapas válidas persiste e retorna sucesso (CA-02)")]
    public async Task Handle_EtapasValidas_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 1, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaNova = processo.Etapas.Single();
        etapaNova.Id.Should().NotBe(etapaOriginal.Id);
        etapaNova.Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "A nota máxima e o passo chegam à etapa criada e à etapa atualizada")]
    public async Task Handle_Escala_ChegaACriadaEAAtualizada()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcesso existente = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([existente], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, existente.Id,
                    Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m, PassoDoLancamento: 0.25m),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2,
                    Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 1000m, PassoDoLancamento: 20m),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, Substitute.For<IUnitOfWork>(), ReaderPadrao());

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Single(e => e.Id == existente.Id).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 10m && e.PassoDoLancamento == 0.25m);
        processo.Etapas.Single(e => e.Nome == "Redação").Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 1000m && e.PassoDoLancamento == 20m);
    }

    [Fact(DisplayName = "Defeitos de escala em etapas diferentes saem numa só resposta, cada um no seu campo")]
    public async Task Handle_DefeitosDeEscalaEmEtapasDiferentes_Acumula()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 0m),
                new EtapaProcessoInput("Entrevista", CaraterEtapa.Eliminatoria, TipoProvaObjetivaOrigemId, null, 12m, 2,
                    Produtos: [], Bancas: [], Recursos: [], NotaMaxima: 10m),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, Substitute.For<IUnitOfWork>(), ReaderPadrao());

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].notaMaxima", "etapas[1].notaMinima"]);
    }

    [Fact(DisplayName = "Handle com o mesmo Id de etapa repetido no payload é recusado")]
    public async Task Handle_ComIdEtapaRepetido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Prova Objetiva (duplicada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 2, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: []),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com ordem de etapa duplicada não persiste (invariante do agregado)")]
    public async Task Handle_OrdemDuplicada_NaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 2m, null, 1, Produtos: [], Bancas: [], Recursos: []),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "ADR-0125: violações de forma acumulam entre etapas, com o índice prefixado ao field, ANTES de consultar o tipoEtapaReader")]
    public async Task Handle_DuasEtapasComViolacaoDeForma_AcumulaComIndicePrefixadoESemConsultarReader()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(" ", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 0m, null, 2, Produtos: [], Bancas: [], Recursos: []),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].nome", "etapas[1].peso"]);
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
