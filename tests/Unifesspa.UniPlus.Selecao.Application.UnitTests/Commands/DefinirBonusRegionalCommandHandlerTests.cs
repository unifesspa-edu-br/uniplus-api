namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class DefinirBonusRegionalCommandHandlerTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal").Value!;

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository, IRegraCatalogoReader RegraCatalogoReader, ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), Substitute.For<ISelecaoUnitOfWork>());
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirBonusRegionalCommand command = new(Guid.CreateVersion7(), null, null, null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com RegraCodigo nulo remove o bônus e persiste (toggle por ausência, RN05)")]
    public async Task Handle_RegraCodigoNulo_RemoveBonus()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        processo.DefinirBonusRegional(ConfiguracaoBonusRegional.Criar(
            Domain.ValueObjects.ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!,
            1.20m, null, null, null).Value!);

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirBonusRegionalCommand command = new(processo.Id, null, null, null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().BeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com regra válida define o bônus e persiste")]
    public async Task Handle_RegraValida_DefineBonus()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TipoProcesso.PSVR);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraBonus));

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, "Marabá", "RN05");

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().NotBeNull();
        processo.BonusRegional!.Fator.Should().Be(1.20m);
        processo.BonusRegional.MunicipioConvenio.Should().Be("Marabá");
    }

    [Fact(DisplayName = "Handle com RegraCodigo informado mas sem Fator recusa")]
    public async Task Handle_SemFator_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirBonusRegionalCommand command = new(processo.Id, "INEXISTENTE", "v1", 1.20m, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de regra_bonus recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraCalculo));

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraTipoInvalido");
    }

    /// <summary>
    /// Monta um processo minimamente conforme e já publicado (CA-07 + RN08) —
    /// mesmo par exigido pelo gate de conformidade de
    /// <see cref="ProcessoSeletivo.Publicar"/>, replicado aqui para exercitar
    /// o bloqueio de mutação pós-publicação (CA-04) no handler.
    /// </summary>
    private static ProcessoSeletivo NovoProcessoPublicado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Publicado", TipoProcesso.SiSU);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        string hashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
        Domain.ValueObjects.ReferenciaRegra regraDistribuicao = Domain.ValueObjects.ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", hashFixo).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021").Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: regraDistribuicao,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao]).IsSuccess.Should().BeTrue();

        Domain.ValueObjects.ReferenciaRegra regraCalculo = Domain.ValueObjects.ReferenciaRegra.Criar(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", hashFixo).Value!;
        Domain.ValueObjects.ReferenciaRegra regraOrdemAlocacao = Domain.ValueObjects.ReferenciaRegra.Criar(
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", hashFixo).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: regraCalculo,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: regraOrdemAlocacao,
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        Domain.ValueObjects.DadosEdital dados = Domain.ValueObjects.DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        byte[] bytesCanonicos = System.Text.Encoding.UTF8.GetBytes(
            new JsonObject { ["status"] = "ok" }.ToJsonString());
        processo.Publicar(dados, bytesCanonicos, "1.0", "canonical-json/sha256@v1", hashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName =
        "Handle com processo já publicado propaga MutacaoPosPublicacaoBloqueada e NÃO persiste (CA-04, achado Codex PR #791)")]
    public async Task Handle_ProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraBonus));

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, "Marabá", "RN05");

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.BonusRegional.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName =
        "Handle de remoção (RegraCodigo nulo) com processo já publicado propaga bloqueio e NÃO persiste (CA-04)")]
    public async Task Handle_RemocaoComProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirBonusRegionalCommand command = new(processo.Id, null, null, null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
