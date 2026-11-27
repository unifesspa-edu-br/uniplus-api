namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A coincidência entre a conferência de publicação e o item de conformidade da âncora do prazo de
/// recurso. <c>FaseCronograma.Criar</c> recusa a regra que ancora fora dos produtos
/// preliminares da própria fase, mas esse estado é <b>materializável</b>: o EF hidrata
/// <c>recursos_configurados_da_fase</c> e <c>publicacoes_previstas_da_fase</c> direto das linhas, sem passar pela
/// fábrica, e <c>publicacao_de_referencia_id</c> não tem chave estrangeira nem CHECK que o prenda.
/// </summary>
/// <remarks>
/// <para>
/// Sem as duas metades, o codificador produz um estado que o próprio decodificador rejeita: a
/// publicação congelaria uma versão cuja âncora não resolve, e a incoerência só apareceria na
/// restauração — longe da causa, e sem o operador ter visto pendência nenhuma no checklist.
/// </para>
/// <para>
/// A prova precisa de Postgres real e de SQL cru: nenhum caminho do domínio produz o estado que
/// se está defendendo. Semear pelo agregado e adulterar a linha depois é o que reproduz a
/// hidratação de dado legado — mesmo raciocínio de
/// <see cref="TaxaInscricaoSemFundamentoPersistenciaTests"/>.
/// </para>
/// </remarks>
public sealed class AncoraDoRecursoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("a712345678", 7))[..64];

    private readonly ProcessoSeletivoDbFixture _fixture;

    public AncoraDoRecursoPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    // A âncora passa a apontar para o produto DEFINITIVO da mesma fase: continua sendo um produto dela, então nenhuma checagem de
    // pertinência sozinha veria o defeito; o que some é a decisão contestável de que o prazo contaria.
    [Fact(DisplayName = "Âncora hidratada sobre produto DEFINITIVO marca o item vermelho e recusa a publicação")]
    public async Task AncoraEmProdutoDefinitivo_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1427 ancora {Guid.CreateVersion7()}");
        await ApontarAncoraParaAsync(processoId, "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo);
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        AfirmarQueAAncoraHidratouNoDefinitivo(processo);
        AfirmarAncoraVermelhaEPublicacaoRecusada(processo);
    }

    private static void AfirmarQueAAncoraHidratouNoDefinitivo(ProcessoSeletivo processo)
    {
        FaseCronograma fase = processo.CronogramaFases.Single();

        fase.RegraRecurso!.ProdutoAncoraId.Should().Be(
            fase.Produtos.Single(p => p.AtoCodigo == "RESULTADO_CLASSIFICACAO" && p.Papel == PapelDaPublicacaoPrevista.Definitivo).Id,
            "pré-condição: o EF hidratou a âncora incoerente sem passar por fábrica nenhuma");
    }

    private static void AfirmarAncoraVermelhaEPublicacaoRecusada(ProcessoSeletivo processo)
    {
        IReadOnlyList<ItemConformidade> checklist = ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(ComCalendario()));

        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_ancora_do_recurso")
            .Which.Ok.Should().BeFalse(
                "quem monta o edital precisa ver a pendência; invariante que só existe no domínio não aparece no checklist");
        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_conclusao_do_ciclo_recursal")
            .Which.Ok.Should().BeTrue("a fase continua concluindo o próprio ciclo — o que quebrou foi a âncora");

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue(
            "linha legada não pode congelar uma versão cujo prazo de interposição não resolve para publicação nenhuma");
        publicacao.Error!.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase",
            "PendenciaDoCronograma precede o agregador genérico, e a causa nomeada é a que orienta");
    }

    [Fact(DisplayName = "Âncora hidratada apontando para produto de OUTRA fase marca o item vermelho e recusa a publicação")]
    public async Task AncoraForaDaFase_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1427 ancora fora {Guid.CreateVersion7()}");

        await ApontarAncoraParaProdutoInexistenteAsync(processoId);

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(ComCalendario()))
            .Should().ContainSingle(i => i.Codigo == "cronograma_ancora_do_recurso")
            .Which.Ok.Should().BeFalse();

        Publicar(processo).IsFailure.Should().BeTrue(
            "sem chave estrangeira, nada no banco impede a linha de apontar para fora da fase — a conferência é a defesa");
    }

    [Fact(DisplayName = "Os procedimentos de recurso da janela da fase voltam do banco como foram gravados, na ordem canônica")]
    public async Task ProcedimentosDeRecurso_GravadosEReidratadosDaColunaJsonb()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS procedimentos {Guid.CreateVersion7()}", ProcedimentosDaJanela());

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.CronogramaFases.Single().RegraRecurso!.Procedimentos.MesmoConteudo(ProcedimentosDaJanela()).Should().BeTrue();
    }

    [Fact(DisplayName = "Contraprova: a mesma configuração com a âncora no preliminar tem o item verde e publica")]
    public async Task AncoraNoPreliminar_ItemVerdeEPublica()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1427 ancora ok {Guid.CreateVersion7()}");

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(ComCalendario()))
            .Should().ContainSingle(i => i.Codigo == "cronograma_ancora_do_recurso")
            .Which.Ok.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
    }

    /// <summary>
    /// Calendário vigente mínimo — o prazo de interposição corre em dia útil, e sem ele a
    /// publicação recusaria por uma pendência que não é a testada aqui.
    /// </summary>
    private static ContextoDeContagemDePrazos ComCalendario() => new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(new CenarioDeCongelamento
    {
        Dados = DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo),
        AtorUsuarioContaId = "teste",
        Contagem = ComCalendario(),
    });

    /// <summary>
    /// Processo estruturalmente publicável cuja única fase publica o preliminar e o definitivo
    /// da matéria e admite recurso ancorado no preliminar — o único jeito de chegar à linha,
    /// porque a fábrica recusa gravar a âncora incoerente.
    /// </summary>
    private async Task<Guid> SemearProcessoConformeAsync(string nome, ProcedimentosDeRecurso? procedimentos = null)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();

        ProcessoSeletivo processo = NovoProcesso(nome);
        DefinirCronogramaEAvaliacao(processo, procedimentos ?? ProcedimentosCoerentesComAFase());
        DefinirVagas(processo);
        DefinirClassificacao(processo);
        DefinirDemaisDeclaracoes(processo);
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        return processo.Id;
    }

    private static ProcessoSeletivo NovoProcesso(string nome) =>
        ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static void DefinirCronogramaEAvaliacao(ProcessoSeletivo processo, ProcedimentosDeRecurso procedimentos)
    {
        processo.DefinirCronogramaFases([FaseConforme(procedimentos)], [])
            .IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();
    }

    private static void DefinirVagas(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), 'a'), Modalidades = [modalidade] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static void DefinirClassificacao(ProcessoSeletivo processo) =>
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), 'b'),
                RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
            }).Value!).IsSuccess.Should().BeTrue();

    private static void DefinirDemaisDeclaracoes(ProcessoSeletivo processo)
    {
        processo.DefinirAlgoritmoContagemPrazo(
            Regra(ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo(), 'd'))
            .IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
    }

    private const string AtualizacaoDaAncoraPorProduto =
        """
        UPDATE selecao.recursos_configurados_da_fase r
           SET publicacao_de_referencia_id = p.id
          FROM selecao.fases_cronograma f, selecao.publicacoes_previstas_da_fase p
         WHERE r.fase_cronograma_id = f.id
           AND p.fase_cronograma_id = f.id
           AND p.ato_codigo = @ato
           AND p.papel = @papel
           AND f.processo_seletivo_id = @processo
        """;

    /// <summary>Reaponta a âncora para o produto da fase que publica <paramref name="atoCodigo"/> com o <paramref name="papel"/>.</summary>
    private Task ApontarAncoraParaAsync(Guid processoId, string atoCodigo, PapelDaPublicacaoPrevista papel) => ExecutarAsync(
        AtualizacaoDaAncoraPorProduto,
        comando =>
        {
            comando.Parameters.AddWithValue("ato", atoCodigo);
            comando.Parameters.AddWithValue("papel", papel.Codigo());
            comando.Parameters.AddWithValue("processo", processoId);
        });

    /// <summary>
    /// Reaponta a âncora para um identificador que não é produto de fase nenhuma — a forma
    /// que uma chave estrangeira impediria, e que sem ela nada no banco recusa.
    /// </summary>
    private Task ApontarAncoraParaProdutoInexistenteAsync(Guid processoId) => ExecutarAsync(
        """
        UPDATE selecao.recursos_configurados_da_fase r
           SET publicacao_de_referencia_id = @forasteiro
          FROM selecao.fases_cronograma f
         WHERE r.fase_cronograma_id = f.id
           AND f.processo_seletivo_id = @processo
        """,
        comando =>
        {
            comando.Parameters.AddWithValue("forasteiro", Guid.CreateVersion7());
            comando.Parameters.AddWithValue("processo", processoId);
        });

    private async Task ExecutarAsync(string sql, Action<NpgsqlCommand> parametrizar)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(sql, conexao);
        parametrizar(comando);

        int afetadas = await comando.ExecuteNonQueryAsync();
        afetadas.Should().Be(1, "a semeadura grava exatamente uma regra de recurso para a única fase do processo");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.Formularios).ThenInclude(f => f.Paginas)
            .Include(p => p.Campos).ThenInclude(f => f.Precondicoes)
            .Include(p => p.AtendimentoEspecializado)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.CandidatosAtendidos)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .Include(p => p.CriteriosDesempate)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static FaseCronograma FaseConforme(ProcedimentosDeRecurso procedimentos)
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar);

        return FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                origemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true,
                coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(
                [preliminar, ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)], RegraDeRecurso(preliminar.Id, procedimentos)) with
            { TiposDeDecisao = TiposDeDecisaoDaFase() },
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), BancasDeTeste.DeRecursosDaFase()]).Value!;
    }

    // A fase declara os tipos que produz, e a conferência da publicação exige procedimento para cada um, sem alvo que a fase não tem.
    private static string[] TiposDeDecisaoDaFase() =>
        [TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.AnaliseDocumental.Codigo(), TipoMotivoDeDecisao.HomologacaoDasInscricoes.Codigo()];

    private static ProcedimentosDeRecurso ProcedimentosCoerentesComAFase() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, Regra(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), 'f')),
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.AnaliseDocumental, null, Regra(RegraDeProcedimentoDoRecurso.Conferencia.Codigo(), 'f')),
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, Regra(RegraDeProcedimentoDoRecurso.CorrecaoMaterial.Codigo(), 'f')),
    ]);

    private static ProcedimentosDeRecurso ProcedimentosDaJanela() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Matematica), Regra(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), 'f')),
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.AnaliseDocumental,
            AlvoDoProcedimentoDeRecurso.DoItem(Guid.Parse("00000000-0000-7000-8000-0000000000b1")),
            Regra(RegraDeProcedimentoDoRecurso.Conferencia.Codigo(), 'f')),
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, Regra(RegraDeProcedimentoDoRecurso.CorrecaoMaterial.Codigo(), 'f')),
    ]);

    private static RegraRecursoFase RegraDeRecurso(Guid produtoPreliminarId, ProcedimentosDeRecurso procedimentos) =>
        RegraRecursoFase.Criar(
            Regra(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), 'e'),
            ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
            produtoPreliminarId,
            procedimentos).Value!;
}
