namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

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

public sealed class DefinirCriteriosDesempateCommandHandlerTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal").Value!;

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IFatoCandidatoReader FatoCandidatoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        IFatoCandidatoReader fatoCandidatoReader = Substitute.For<IFatoCandidatoReader>();
        fatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(
            (IReadOnlyList<FatoCandidatoView>)
            [
                new FatoCandidatoView(Guid.CreateVersion7(), "PROFESSOR_RURAL", "Professor da rede pública rural", null, "BOOLEANO", "BRUTO_INFORMADO", "ESCALAR", null),
            ]);

        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), fatoCandidatoReader, Substitute.For<ISelecaoUnitOfWork>());
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirCriteriosDesempateCommand command = new(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-ETAPA resolve args e persiste")]
    public async Task Handle_MaiorNotaEtapa_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaProcesso.Criar("Entrevista", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaEtapa, "v1", etapa.Id, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().ContainSingle();
        ((ArgsDesempateMaiorNotaEtapa)processo.CriteriosDesempate.Single().Args).EtapaRef.Should().Be(etapa.Id);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-ETAPA sem EtapaRef recusa")]
    public async Task Handle_MaiorNotaEtapaSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorNotaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorNotaEtapa, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorNotaEtapa, "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.EtapaRefObrigatorio");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-IDOSO resolve args e persiste")]
    public async Task Handle_Idoso_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.Idoso, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.Idoso, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.Idoso, "v1", null, 60, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ((ArgsDesempateIdoso)processo.CriteriosDesempate.Single().Args).IdadeMinima.Should().Be(60);
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO sobre fato do vocabulário fechado resolve e persiste")]
    public async Task Handle_PredicadoFato_FatoConhecido_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "PROFESSOR_RURAL", "IGUAL", "true")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ArgsDesempatePredicadoFato args = (ArgsDesempatePredicadoFato)processo.CriteriosDesempate.Single().Args;
        args.Condicao.Fato.Should().Be("PROFESSOR_RURAL");
        args.Condicao.Valor.GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO sobre fato fora do vocabulário fechado recusa")]
    public async Task Handle_PredicadoFato_FatoDesconhecido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "FATO_INEXISTENTE", "IGUAL", "S")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO incompleto recusa")]
    public async Task Handle_PredicadoFatoIncompleto_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSE 2026", TipoProcesso.PSECampo);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.PredicadoFato, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.PredicadoFato, TipoRegra.CriterioDesempate));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "PROFESSOR_RURAL", null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.PredicadoFatoIncompleto");
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, "INEXISTENTE", "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de criterio_desempate recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(CriterioDesempateCodigo.MaiorIdade, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(CriterioDesempateCodigo.MaiorIdade, TipoRegra.RegraCalculo));

        DefinirCriteriosDesempateCommand command = new(
            processo.Id, [new CriterioDesempateInput(1, CriterioDesempateCodigo.MaiorIdade, "v1", null, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraTipoInvalido");
    }

    [Fact(DisplayName = "Handle com lista vazia remove todos os critérios e persiste")]
    public async Task Handle_ListaVazia_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirCriteriosDesempateCommand command = new(processo.Id, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCriteriosDesempateCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.FatoCandidatoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().BeEmpty();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
