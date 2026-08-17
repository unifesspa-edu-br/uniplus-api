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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do <see cref="DefinirCascataRemanejamentoCommandHandler"/>
/// (RN-CASCATA-4/5, Story #575): remoção por ausência total, recusa de
/// presença parcial, resolução da regra no catálogo, e a ordem
/// forma-antes-de-conteúdo — a factory (RN-CASCATA-4) roda antes da
/// comparação célula a célula contra o <c>esquema_args</c> (RN-CASCATA-5),
/// para que um erro de forma nunca seja mascarado por
/// <c>MatrizDivergenteDaRegra</c>.
/// </summary>
public sealed class DefinirCascataRemanejamentoCommandHandlerTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo RegraCascataValida() => RegraCatalogo.Criar(
        RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.CriterioRemanejamento,
        Json("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q","LB_PCD"]}]}"""),
        Json("[]"), "ADR-0120").Value!;

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository, IRegraCatalogoReader RegraCatalogoReader, ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), Substitute.For<ISelecaoUnitOfWork>());
    }

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirCascataRemanejamentoCommand command = new(Guid.CreateVersion7(), null, null, null, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com os quatro campos nulos remove a cascata, sem consultar o catálogo de regras")]
    public async Task Handle_TodosOsCamposNulos_Remove()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoCascataRemanejamento cascataExistente = ConfiguracaoCascataRemanejamento.Criar(
            ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo).Value!,
            "AC", [DestinoRemanejamento.Criar("LB_PPI", 1, "LB_Q").Value!]).Value!;
        processo.DefinirCascataRemanejamento(cascataExistente, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCascataRemanejamentoCommand command = new(processo.Id, null, null, null, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Cascata.Should().BeNull();
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    public static TheoryData<string?, string?, string?, IReadOnlyList<DestinoRemanejamentoInput>?> CombinacoesParciais() =>
        new()
        {
            { RegraRemanejamentoCodigo.Cascata, null, null, null },
            { null, "v1", null, null },
            { null, null, "AC", null },
            { null, null, null, [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")] },
            { RegraRemanejamentoCodigo.Cascata, "v1", null, null },
            { RegraRemanejamentoCodigo.Cascata, "v1", "AC", null },
        };

    [Theory(DisplayName = "Handle com combinação parcial dos quatro campos recusa com CamposObrigatorios")]
    [MemberData(nameof(CombinacoesParciais))]
    public async Task Handle_CombinacaoParcial_Recusa(
        string? regraCodigo, string? regraVersao, string? fallbackCodigo, IReadOnlyList<DestinoRemanejamentoInput>? destinos)
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, regraCodigo, regraVersao, fallbackCodigo, destinos, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com regra inexistente no catálogo recusa com RegraNaoEncontrada")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, "INEXISTENTE", "v1", "AC", [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de criterio_remanejamento recusa com RegraTipoInvalido")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        RegraCatalogo regraTipoErrado = RegraCatalogo.Criar(
            RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.RegraCalculo, Json("{}"), Json("[]"), "base legal").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(regraTipoErrado);

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC", [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.RegraTipoInvalido");
    }

    [Fact(DisplayName = "Handle com ordem inválida (RN-CASCATA-4) recusa pela factory, ANTES de comparar contra o esquema_args (achado rodada 3)")]
    public async Task Handle_OrdemInvalida_RecusaPelaFactoryAntesDoEsquemaArgs()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        // Ordem 0 é inválida na FORMA (RN-CASCATA-4) — mesmo que o resto da matriz
        // divergisse do esquema_args também, o erro que deve aparecer é o de forma.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 0, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.OrdemInvalida");
    }

    [Fact(DisplayName = "Handle com fallback divergente do esquema_args recusa com MatrizDivergenteDaRegra, mesmo com forma válida")]
    public async Task Handle_FallbackDivergenteDoEsquemaArgs_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        // Forma válida (RN-CASCATA-4 passa), mas o fallback ("AMPLA", não "AC") diverge
        // do esquema_args congelado da regra.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AMPLA",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_PCD")],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com destino trocado em relação ao esquema_args recusa com MatrizDivergenteDaRegra")]
    public async Task Handle_DestinoTrocadoNoEsquemaArgs_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        // O esquema_args de RegraCascataValida() é LB_PPI → [LB_Q, LB_PCD]; aqui a
        // ordem 2 aponta para LB_EP em vez de LB_PCD — um destino trocado.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_EP")],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com linha de origem omitida em relação ao esquema_args recusa com MatrizDivergenteDaRegra")]
    public async Task Handle_OrigemOmitidaNoEsquemaArgs_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        RegraCatalogo regraComDuasOrigens = RegraCatalogo.Criar(
            RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.CriterioRemanejamento,
            Json("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q"]},{"origem":"LB_Q","destinos":["LB_PPI"]}]}"""),
            Json("[]"), "ADR-0120").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(regraComDuasOrigens);

        // O payload aplicado só declara LB_PPI — omite a linha de LB_Q que o
        // esquema_args da regra também exige.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com esquema_args repetindo uma origem não deixa outra origem sem conferir (achado de revisão)")]
    public async Task Handle_EsquemaArgsComOrigemRepetida_NaoCobreOrigemNaoConferida()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        // O esquema_args declara LB_PPI DUAS vezes e nunca declara LB_Q — se a comparação
        // fosse por CONTAGEM de linhas em vez de pelo CONJUNTO de origens conferidas, as duas
        // entradas de LB_PPI bateriam com as duas origens do payload (LB_PPI e LB_Q), e o
        // destino arbitrário de LB_Q no payload passaria sem nunca ser comparado a nada.
        RegraCatalogo regraComOrigemRepetida = RegraCatalogo.Criar(
            RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.CriterioRemanejamento,
            Json("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q"]},{"origem":"LB_PPI","destinos":["LB_Q"]}]}"""),
            Json("[]"), "ADR-0120").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(regraComOrigemRepetida);

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [
                new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"),
                new DestinoRemanejamentoInput("LB_Q", 1, "DESTINO_NUNCA_CONFERIDO"),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue("LB_Q nunca foi comparado contra o esquema_args — a origem repetida não pode cobri-la");
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com item nulo na lista de destinos recusa com CamposObrigatorios, sem lançar (achado de revisão)")]
    public async Task Handle_ItemNuloEmDestinos_RecusaSemLancar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [null!], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com regra válida e matriz batendo célula a célula define a cascata e persiste")]
    public async Task Handle_RegraValidaEMatrizBatendo_DefineEPersiste()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_PCD")],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Cascata.Should().NotBeNull();
        processo.Cascata!.FallbackCodigo.Should().Be("AC");
        processo.Cascata.Destinos.Should().HaveCount(2);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Processo publicado, sem sessão editorial aberta — o par mínimo exigido por <see cref="ProcessoSeletivo.MutacaoBloqueada"/> (ADR-0110).</summary>
    private static ProcessoSeletivo NovoProcessoPublicado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Publicado", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, regraDistribuicao, regraAjuste: null, referenciaDemografica: null, [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null, casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
            nOpcoesAlocacao: 1, regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria, agrupaEtapas: true, permiteComplementacao: false, produzResultado: true,
            resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;
        byte[] bytesCanonicos = System.Text.Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());
        processo.Publicar(dados, bytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-sub-123", TimeProvider.System)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "ADR-0125: Handle acumula violações de itens distintos com o índice do item prefixado ao field")]
    public async Task Handle_DoisItensInvalidos_AcumulaComIndicePrefixadoAoField()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("lb-ppi", 0, "LB_PCD")],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(
        [
            "destinos[1].modalidadeOrigemCodigo",
            "destinos[1].ordem",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: Handle acumula fallback inválido junto com item malformado, mesmo sem checagem de coerência entre itens (achado de revisão)")]
    public async Task Handle_FallbackInvalidoEItemMalformado_AcumulaOsDois()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC-1",
            [new DestinoRemanejamentoInput("LB_PPI", 0, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoCascataRemanejamento.FallbackObrigatorio",
            "ConfiguracaoCascataRemanejamento.OrdemInvalida",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: validação de forma precede a consulta ao catálogo de regras, mesmo com regra inexistente (achado de revisão)")]
    public async Task Handle_FallbackInvalidoComRegraInexistente_ValidacaoVenceAConsultaAoCatalogo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, "INEXISTENTE", "v1", "ac-invalido",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.FallbackObrigatorio");
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Handle com processo já publicado, sem sessão editorial, propaga bloqueio e NÃO persiste (CA-07/ADR-0110)")]
    public async Task Handle_ProcessoPublicadoSemSessao_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.Cascata.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
