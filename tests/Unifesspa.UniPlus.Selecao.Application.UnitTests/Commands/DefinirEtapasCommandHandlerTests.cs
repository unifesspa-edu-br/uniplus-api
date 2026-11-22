namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
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
        repository.ObterParaMutacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(
            Guid.CreateVersion7(), [EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)]);

        Result result = await Executar(command, repository, unitOfWork);

        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    // O agregado é tracked (ObterParaMutacaoAsync); a persistência é por change detection no SaveChanges —
    // NÃO se chama DbSet.Update (que marcaria os filhos novos com Guid v7 como Modified → UPDATE inválido).
    [Fact(DisplayName = "Handle com etapas válidas persiste e retorna sucesso (CA-02)")]
    public async Task Handle_EtapasValidas_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = RepositorioCom(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(processo.Id, [EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)]);

        Result result = await Executar(command, repository, unitOfWork);

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva");
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        repository.DidNotReceive().Atualizar(Arg.Any<ProcessoSeletivo>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O sucesso devolve a etapa gravada com o identificador gerado")]
    public async Task Handle_EtapaValida_DevolveEtapaGravada()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = RepositorioCom(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(processo.Id, [EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1)]);

        Result<IReadOnlyList<EtapaProcessoDto>> result = await DefinirEtapasCommandHandler.Handle(
            command, repository, new CadastrosDeApoioDeDefinirEtapas().Definicao(ReaderPadrao(), repository), unitOfWork, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        EtapaProcessoDto etapa = result.Value!.Should().ContainSingle().Subject;
        etapa.Id.Should().Be(processo.Etapas.Single().Id).And.NotBeEmpty();
        etapa.Identificacao.Nome.Should().Be(processo.Etapas.Single().Nome);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Id de etapa existente atualiza a MESMA instância (preserva etapa_ref)")]
    public async Task Handle_ComIdExistente_AtualizaMesmaInstancia()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);

        Result result = await ExecutarSobre(
            processo, null, EtapaInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1, etapaOriginal.Id));

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaAtualizada = processo.Etapas.Single();
        etapaAtualizada.Should().BeSameAs(etapaOriginal);
        etapaAtualizada.Nome.Should().Be("Prova Objetiva (revisada)");
        etapaAtualizada.Peso.Should().Be(3m);
    }

    [Fact(DisplayName = "Handle sem Id (ou com Id sem correspondência) cria etapa nova")]
    public async Task Handle_SemId_CriaEtapaNova()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);

        Result result = await ExecutarSobre(
            processo, null, EtapaInput("Entrevista", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 1));

        result.IsSuccess.Should().BeTrue();
        EtapaProcesso etapaNova = processo.Etapas.Single();
        etapaNova.Id.Should().NotBe(etapaOriginal.Id);
        etapaNova.Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "A nota máxima e o passo chegam à etapa criada e à etapa atualizada")]
    public async Task Handle_Escala_ChegaACriadaEAAtualizada()
    {
        EtapaProcesso existente = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(existente);

        Result result = await ExecutarSobre(
            processo, null,
            EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, existente.Id) with { NotaMaxima = 10m, PassoDoLancamento = 0.25m },
            EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2) with { NotaMaxima = 1000m, PassoDoLancamento = 20m });

        result.IsSuccess.Should().BeTrue();
        processo.Etapas.Single(e => e.Id == existente.Id).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 10m && e.PassoDoLancamento == 0.25m);
        processo.Etapas.Single(e => e.Nome == "Redação").Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 1000m && e.PassoDoLancamento == 20m);
    }

    [Fact(DisplayName = "Defeitos de escala em etapas diferentes saem numa só resposta, cada um no seu campo")]
    public async Task Handle_DefeitosDeEscalaEmEtapasDiferentes_Acumula()
    {
        Result result = await ExecutarSobre(
            ProcessoSemEtapas(), null,
            EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1) with { NotaMaxima = 0m },
            EtapaInput("Entrevista", CaraterEtapa.Eliminatoria, TipoProvaObjetivaOrigemId, null, 12m, 2) with { NotaMaxima = 10m });

        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].notaMaxima", "etapas[1].notaMinima"]);
    }

    [Fact(DisplayName = "Handle com o mesmo Id de etapa repetido no payload é recusado")]
    public async Task Handle_ComIdEtapaRepetido_Recusa()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(
            processo.Id,
            [
                EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id),
                EtapaInput("Prova Objetiva (duplicada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 2m, null, 2, etapaOriginal.Id),
            ]);

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork);

        result.Error!.Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com ordem de etapa duplicada não persiste (invariante do agregado)")]
    public async Task Handle_OrdemDuplicada_NaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(
            processo.Id,
            [
                EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1),
                EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 2m, null, 1),
            ]);

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork);

        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "ADR-0125: violações de forma acumulam entre etapas, com o índice prefixado ao field, ANTES de consultar o tipoEtapaReader")]
    public async Task Handle_DuasEtapasComViolacaoDeForma_AcumulaComIndicePrefixadoESemConsultarReader()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(processo.Id, EtapasComNomeEmBrancoEPesoZero());

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork, tipoEtapaReader);

        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].nome", "etapas[1].peso"]);
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static EtapaProcessoInput[] EtapasComNomeEmBrancoEPesoZero() =>
    [
        EtapaInput(" ", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 3m, null, 1),
        EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 0m, null, 2),
    ];
}
