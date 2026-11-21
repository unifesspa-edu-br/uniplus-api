namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
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
/// A coincidência entre a conferência de publicação e o item de conformidade dos candidatos atendidos
/// pelas bancas requeridas. <c>FaseCronograma.Criar</c> recusa duas bancas do
/// mesmo tipo que não se distinguem, mas esse estado é <b>materializável</b>: o EF hidrata
/// <c>bancas_requeridas</c> e <c>categorias_julgadas</c> direto das linhas, sem passar pela
/// fábrica, e nada no banco obriga uma banca a ter candidatos atendidos nem impede duas do mesmo tipo de
/// terem os mesmos.
/// </summary>
/// <remarks>
/// <para>
/// Sem as duas metades, um certame carregado do banco nesse estado seria publicado sem dizer
/// quem responde por qual matéria, e o defeito só apareceria no primeiro parecer ou no
/// primeiro recurso — longe da causa, e sem o operador ter visto pendência no checklist.
/// </para>
/// <para>
/// A prova precisa de Postgres real e de SQL cru: nenhum caminho do domínio produz o estado
/// que se está defendendo.
/// </para>
/// </remarks>
public sealed class CandidatosAtendidosConferenciaPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("a712345678", 7))[..64];

    /// <summary>
    /// O tipo de banca é o MESMO nas duas bancas da fase semeada — é esse empate que faz os
    /// candidatos atendidos serem o único separador entre elas.
    /// </summary>
    private static readonly Guid TipoBancaOrigemId = Guid.CreateVersion7();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public CandidatosAtendidosConferenciaPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Banca hidratada sem candidatos atendidos, ao lado de outra do mesmo tipo, marca o item pendente e recusa a publicação")]
    public async Task BancaSemCandidatosAtendidos_ItemPendenteEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1428 sem candidatos atendidos {Guid.CreateVersion7()}");
        await ApagarCandidatosAtendidosDeAsync(processoId, "ETNICO_RACIAL");
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);
        AfirmarQueABancaHidratouSemCandidatosAtendidos(processo);
        AfirmarOItemPendenteDosCandidatosAtendidos(processo);

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue(
            "linha legada não pode congelar um edital em que duas bancas do mesmo tipo são a mesma linha repetida");
        publicacao.Error!.Code.Should().Be("BancaRequerida.CandidatosAtendidosObrigatorios");
    }

    private static void AfirmarQueABancaHidratouSemCandidatosAtendidos(ProcessoSeletivo processo) =>
        processo.CronogramaFases.Single().BancasRequeridas
            .Should().Contain(b => b.CandidatosAtendidos.Count == 0,
                "pré-condição: o EF hidratou a banca sem candidatos atendidos sem passar por fábrica nenhuma");

    private static void AfirmarOItemPendenteDosCandidatosAtendidos(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(ComCalendario()))
            .Should().ContainSingle(i => i.Codigo == "cronograma_candidatos_atendidos_das_bancas")
            .Which.Ok.Should().BeFalse(
                "quem monta o edital precisa ver a pendência; invariante que só existe no domínio não aparece no checklist");

    [Fact(DisplayName = "Duas bancas do mesmo tipo hidratadas com os MESMOS candidatos atendidos marcam o item pendente e recusam a publicação")]
    public async Task CandidatosAtendidosIguais_ItemPendenteEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1428 candidatos atendidos iguais {Guid.CreateVersion7()}");
        await UniformizarCandidatosAtendidosAsync(processoId);
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);
        ConferirQueAsBancasDocumentaisSeguemComCandidatosAtendidos(processo);
        ItemDosCandidatosAtendidosDasBancas(processo).Ok.Should().BeFalse();
        Result<VersaoConfiguracao> publicacao = Publicar(processo);
        publicacao.IsFailure.Should().BeTrue();
        publicacao.Error!.Code.Should().Be("BancaRequerida.CandidatosAtendidosDuplicados");
    }

    [Fact(DisplayName = "Contraprova: a mesma configuração com candidatos atendidos distintos tem o item conforme e publica")]
    public async Task CandidatosAtendidosDistintos_ItemConformeEPublica()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1428 candidatos atendidos ok {Guid.CreateVersion7()}");

        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        ItemDosCandidatosAtendidosDasBancas(processo).Ok.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
    }

    /// <summary>Apaga os candidatos atendidos da banca que julga <paramref name="codigo"/>, deixando-a sem nenhum.</summary>
    private Task ApagarCandidatosAtendidosDeAsync(Guid processoId, string codigo) => ExecutarAsync(
        """
        DELETE FROM selecao.categorias_julgadas c
         USING selecao.bancas_requeridas b, selecao.fases_cronograma f
         WHERE c.banca_requerida_id = b.id
           AND b.fase_cronograma_id = f.id
           AND c.codigo = @codigo
           AND f.processo_seletivo_id = @processo
        """,
        comando =>
        {
            comando.Parameters.AddWithValue("codigo", codigo);
            comando.Parameters.AddWithValue("processo", processoId);
        },
        linhasEsperadas: 1);

    /// <summary>
    /// Faz as duas bancas do mesmo tipo julgarem a MESMA categoria: cada uma tem candidatos atendidos, então a
    /// checagem de ausência sozinha não veria o defeito — o que some é a distinção.
    /// </summary>
    private Task UniformizarCandidatosAtendidosAsync(Guid processoId) => ExecutarAsync(
        """
        UPDATE selecao.categorias_julgadas c
           SET codigo = 'RENDA'
          FROM selecao.bancas_requeridas b, selecao.fases_cronograma f
         WHERE c.banca_requerida_id = b.id
           AND b.fase_cronograma_id = f.id
           AND c.codigo = 'ETNICO_RACIAL'
           AND f.processo_seletivo_id = @processo
        """,
        comando => comando.Parameters.AddWithValue("processo", processoId),
        linhasEsperadas: 1);

    private static ItemConformidade ItemDosCandidatosAtendidosDasBancas(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(ComCalendario()))
            .Should().ContainSingle(i => i.Codigo == "cronograma_candidatos_atendidos_das_bancas").Which;

    private static void ConferirQueAsBancasDocumentaisSeguemComCandidatosAtendidos(ProcessoSeletivo processo) =>
        processo.CronogramaFases.Single().BancasRequeridas.Where(b => b.Codigo == "BANCA_ANALISE_DOCUMENTAL")
            .Should().OnlyContain(b => b.CandidatosAtendidos.Count == 1,
                "pré-condição: as duas bancas seguem com candidatos atendidos declarados — o que mudou foi serem os mesmos");

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
    private async Task<Guid> SemearProcessoConformeAsync(string nome)
    {
        await using UniPlusDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoComCronogramaEEtapa(nome);
        DefinirAsVagas(processo);
        DefinirAClassificacao(processo);
        DefinirAsDeclaracoesObrigatorias(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        return processo.Id;
    }

    private static ProcessoSeletivo ProcessoSiSuSemConfiguracao(string nome) => ProcessoSeletivo.Criar(
        nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static ProcessoSeletivo ProcessoComCronogramaEEtapa(string nome)
    {
        ProcessoSeletivo processo = ProcessoSiSuSemConfiguracao(nome);

        processo.DefinirCronogramaFases([FaseConforme()], [])
            .IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static void DefinirAsVagas(ProcessoSeletivo processo)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [modalidade] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static void DefinirAClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
                RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            }).Value!).IsSuccess.Should().BeTrue();
    }

    private static void DefinirAsDeclaracoesObrigatorias(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirAlgoritmoContagemPrazo(
            Regra(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, 'd'))
            .IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private async Task ExecutarAsync(string sql, Action<NpgsqlCommand> parametrizar, int linhasEsperadas)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(sql, conexao);
        parametrizar(comando);

        int afetadas = await comando.ExecuteNonQueryAsync();
        afetadas.Should().Be(linhasEsperadas, "a adulteração precisa alcançar exatamente as linhas que o cenário descreve");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(UniPlusDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.Formularios).ThenInclude(f => f.Etapas)
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

    private static FaseCronograma FaseConforme()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        return FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
                ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(
                [preliminar, ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], RegraDeRecurso(preliminar.Id)),
            bancasRequeridas: [.. BancasDoMesmoTipoComCandidatosAtendidosDistintos(), BancasDeTeste.DeRecursosDaFase()]).Value!;
    }

    private static BancaRequerida[] BancasDoMesmoTipoComCandidatosAtendidosDistintos() =>
    [
        BancaDosCandidatosAtendidos("RENDA"),
        BancaDosCandidatosAtendidos("ETNICO_RACIAL"),
    ];

    private static BancaRequerida BancaDosCandidatosAtendidos(string categoriaCodigo) =>
        BancaRequerida.Criar(
            TipoBancaOrigemId,
            "BANCA_ANALISE_DOCUMENTAL",
            "Banca de teste",
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), categoriaCodigo)],
            BancasDeTeste.Regra());

    private static RegraRecursoFase RegraDeRecurso(Guid produtoPreliminarId) =>
        RegraRecursoFase.Criar(
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'e'),
            ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
            produtoPreliminarId,
            HomologacaoDasInscricoesDeTeste.ProcedimentosDaHomologacao()).Value!;
}
