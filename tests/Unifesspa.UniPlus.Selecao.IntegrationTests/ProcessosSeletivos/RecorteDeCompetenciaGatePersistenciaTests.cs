namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A bicondicional entre o gate de publicação e o item de conformidade do recorte de
/// competência das bancas requeridas. <c>FaseCronograma.Criar</c> recusa duas bancas do
/// mesmo tipo que não se distinguem, mas esse estado é <b>materializável</b>: o EF hidrata
/// <c>bancas_requeridas</c> e <c>categorias_julgadas</c> direto das linhas, sem passar pela
/// fábrica, e nada no banco obriga uma banca a ter recorte nem impede duas do mesmo tipo de
/// terem o mesmo.
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
public sealed class RecorteDeCompetenciaGatePersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("a712345678", 7))[..64];

    /// <summary>
    /// O tipo de banca é o MESMO nas duas bancas da fase semeada — é esse empate que faz o
    /// recorte ser o único separador entre elas.
    /// </summary>
    private static readonly Guid TipoBancaOrigemId = Guid.CreateVersion7();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RecorteDeCompetenciaGatePersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Banca hidratada sem recorte, ao lado de outra do mesmo tipo, marca o item vermelho e recusa a publicação")]
    public async Task BancaSemRecorte_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1428 sem recorte {Guid.CreateVersion7()}");

        await ApagarRecorteDeAsync(processoId, "ETNICO_RACIAL");

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.CronogramaFases.Single().BancasRequeridas
            .Should().Contain(b => b.RecorteDeCompetencia.Count == 0,
                "pré-condição: o EF hidratou a banca sem recorte sem passar por fábrica nenhuma");

        processo.AvaliarConformidade(ComCalendario())
            .Should().ContainSingle(i => i.Codigo == "cronograma_recorte_de_competencia_das_bancas")
            .Which.Ok.Should().BeFalse(
                "quem monta o edital precisa ver a pendência; invariante que só existe no domínio não aparece no checklist");

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue(
            "linha legada não pode congelar um edital em que duas bancas do mesmo tipo são a mesma linha repetida");
        publicacao.Error!.Code.Should().Be("BancaRequerida.RecorteDeCompetenciaObrigatorio");
    }

    [Fact(DisplayName = "Duas bancas do mesmo tipo hidratadas com o MESMO recorte marcam o item vermelho e recusam a publicação")]
    public async Task RecortesIguais_ItemVermelhoEPublicacaoRecusada()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1428 recorte igual {Guid.CreateVersion7()}");

        // As duas bancas passam a julgar a MESMA categoria: cada uma tem recorte, então a
        // checagem de ausência sozinha não veria o defeito — o que some é a distinção.
        await UniformizarRecortesAsync(processoId);

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.CronogramaFases.Single().BancasRequeridas
            .Should().OnlyContain(b => b.RecorteDeCompetencia.Count == 1,
                "pré-condição: as duas bancas seguem com recorte declarado — o que mudou foi ele ser o mesmo");

        processo.AvaliarConformidade(ComCalendario())
            .Should().ContainSingle(i => i.Codigo == "cronograma_recorte_de_competencia_das_bancas")
            .Which.Ok.Should().BeFalse();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsFailure.Should().BeTrue();
        publicacao.Error!.Code.Should().Be("BancaRequerida.RecorteDeCompetenciaDuplicado");
    }

    [Fact(DisplayName = "Contraprova: a mesma configuração com recortes distintos tem o item verde e publica")]
    public async Task RecortesDistintos_ItemVerdeEPublica()
    {
        Guid processoId = await SemearProcessoConformeAsync($"PS 1428 recorte ok {Guid.CreateVersion7()}");

        await using SelecaoDbContext db = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(db, processoId);

        processo.AvaliarConformidade(ComCalendario())
            .Should().ContainSingle(i => i.Codigo == "cronograma_recorte_de_competencia_das_bancas")
            .Which.Ok.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = Publicar(processo);

        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
    }

    /// <summary>Apaga o recorte da banca que julga <paramref name="codigo"/>, deixando-a sem nenhum.</summary>
    private Task ApagarRecorteDeAsync(Guid processoId, string codigo) => ExecutarAsync(
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

    /// <summary>Faz as duas bancas do mesmo tipo julgarem a MESMA categoria.</summary>
    private Task UniformizarRecortesAsync(Guid processoId) => ExecutarAsync(
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

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        DadosEdital.Criar(
            "001/2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            Guid.CreateVersion7()).Value!,
        "{}"u8.ToArray(),
        "1.1",
        "canonical-json/sha256@v1",
        HashFixo,
        "teste",
        TimeProvider.System,
        ComCalendario());

    /// <summary>
    /// Processo estruturalmente publicável cuja única fase publica o preliminar e o definitivo
    /// da matéria e admite recurso ancorado no preliminar — o único jeito de chegar à linha,
    /// porque a fábrica recusa gravar a âncora incoerente.
    /// </summary>
    private async Task<Guid> SemearProcessoConformeAsync(string nome)
    {
        await using SelecaoDbContext db = _fixture.CreateDbContext();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, notaMinima: null, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                null, null, [modalidade]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, [], baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirAlgoritmoContagemPrazo(
            Regra(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, 'd'), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();

        return processo.Id;
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

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext db, Guid processoId) =>
        await db.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.Classificacao)
            .Include(p => p.OfertaAtendimento)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == processoId);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static FaseCronograma FaseConforme()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar);

        return FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            coletaInscricao: true,
            coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_FINAL", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null,
            emiteParecerIndividual: false,
            bancasRequeridas:
            [
                BancaRequerida.Criar(TipoBancaOrigemId, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")]),
                BancaRequerida.Criar(TipoBancaOrigemId, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(Guid.CreateVersion7(), "ETNICO_RACIAL")]),
            ],
            regraRecurso: RegraRecursoFase.Criar(
                Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, 'e'),
                new ArgsRegraPrazoRecurso(
                    PrazoValor: 48m,
                    PrazoUnidade: UnidadePrazo.Horas,
                    SuspensividadePrimeiraInstanciaValor: null,
                    SuspensividadePrimeiraInstanciaUnidade: null,
                    SuspensividadeSegundaInstanciaValor: null,
                    SuspensividadeSegundaInstanciaUnidade: null),
                preliminar.Id).Value!).Value!;
    }
}
