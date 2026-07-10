namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Integração do cadastro de tipos de ato contra Postgres real: persistência e
/// auditoria, a exclusion constraint que impede duas versões vivas do mesmo código
/// valerem no mesmo dia, os CHECKs de formato e de janela, e a resolução da versão
/// vigente numa data.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoAtoPublicadoPersistenceTests
{
    private const string Admin = "admin-publicacoes";
    private const string ExclusionViolation = "23P01";
    private const string CheckViolation = "23514";
    private const string NomeDaExclusionConstraint = "ex_tipo_ato_publicado_codigo_vigencia";

    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Meio = new(2026, 6, 1);
    private static readonly DateOnly Fim = new(2027, 1, 1);

    private readonly PublicacoesDbFixture _fixture;

    public TipoAtoPublicadoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persiste os campos e a autoria da criação")]
    public async Task Insert_PersisteCamposEAutoria()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado tipo = Novo(codigo, Inicio, vigenciaFim: null, congela: true, irreversivel: true);

        await Gravar(tipo);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        TipoAtoPublicado persistido = await ctx.TiposAtoPublicado.SingleAsync(t => t.Id == tipo.Id);

        persistido.Codigo.Should().Be(codigo);
        persistido.CongelaConfiguracao.Should().BeTrue();
        persistido.EfeitoIrreversivel.Should().BeTrue();
        persistido.VigenciaFim.Should().BeNull();
        persistido.CreatedBy.Should().Be(Admin);
        persistido.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "A exclusion constraint existe no banco como constraint de exclusão")]
    public async Task ExclusionConstraint_ExisteNoCatalogo()
    {
        // A constraint vive em SQL cru, fora do ModelSnapshot: um squash de migrations
        // a descartaria em silêncio, e todos os testes de sobreposição continuariam
        // passando pelo guard de aplicação. Esta asserção olha o catálogo do Postgres.
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT contype::text
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'publicacoes'
              AND t.relname = 'tipo_ato_publicado'
              AND c.conname = 'ex_tipo_ato_publicado_codigo_vigencia'
            """,
            (NpgsqlConnection)ctx.Database.GetDbConnection());

        object? contype = await command.ExecuteScalarAsync();

        contype.Should().Be("x", "'x' é o contype de EXCLUDE no pg_constraint");
    }

    [Fact(DisplayName = "Duas versões vivas do mesmo código com janelas que se cruzam são recusadas")]
    public async Task Insert_ComVigenciaSobreposta_Falha()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, Fim));

        // [2026-06-01, 2027-06-01) cruza [2026-01-01, 2027-01-01).
        Func<Task> segundaInsercao = () => Gravar(Novo(codigo, Meio, Fim.AddYears(1)));

        DbUpdateException erro = (await segundaInsercao.Should().ThrowAsync<DbUpdateException>()).Which;
        PostgresException pg = erro.InnerException.Should().BeOfType<PostgresException>().Which;
        pg.SqlState.Should().Be(ExclusionViolation);

        // O nome da constraint é o que a camada Application usa para distinguir esta
        // violação de qualquer outra exclusion constraint futura. Se o PostgreSQL
        // deixasse de informá-lo, a tradução para erro de domínio falharia em silêncio
        // e a corrida viraria 500.
        pg.ConstraintName.Should().Be(NomeDaExclusionConstraint);
    }

    [Fact(DisplayName = "Vigência aberta cruza qualquer janela futura do mesmo código")]
    public async Task Insert_ComVigenciaAbertaEOutraFutura_Falha()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, vigenciaFim: null));

        Func<Task> segundaInsercao = () => Gravar(Novo(codigo, Fim, vigenciaFim: null));

        DbUpdateException erro = (await segundaInsercao.Should().ThrowAsync<DbUpdateException>()).Which;
        erro.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(ExclusionViolation);
    }

    [Fact(DisplayName = "Sucessão adjacente é aceita e, no dia da fronteira, vigora exatamente uma versão")]
    public async Task Insert_ComJanelasAdjacentes_AceitaEResolveUmaSo()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado antiga = Novo(codigo, Inicio, Meio, congela: false);
        TipoAtoPublicado nova = Novo(codigo, Meio, vigenciaFim: null, congela: true);

        await Gravar(antiga);
        await Gravar(nova);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new TipoAtoPublicadoRepository(ctx, TimeProvider.System);

        // Véspera: a antiga. Dia da fronteira: já a nova — o fim é exclusivo.
        (await repo.ObterVigenteAsync(codigo, Meio.AddDays(-1), default))!.Id.Should().Be(antiga.Id);
        (await repo.ObterVigenteAsync(codigo, Meio, default))!.Id.Should().Be(nova.Id);
        (await repo.ObterVigenteAsync(codigo, Meio.AddYears(10), default))!.Id.Should().Be(nova.Id);
    }

    [Fact(DisplayName = "A versão vigente numa data histórica é a que valia então")]
    public async Task ObterVigente_EmDataHistorica_DevolveVersaoDaEpoca()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado antiga = Novo(codigo, Inicio, Meio, congela: false);
        await Gravar(antiga);
        await Gravar(Novo(codigo, Meio, vigenciaFim: null, congela: true));

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new TipoAtoPublicadoRepository(ctx, TimeProvider.System);

        TipoAtoPublicado? vigenteEmMarco = await repo.ObterVigenteAsync(codigo, new DateOnly(2026, 3, 15), default);

        vigenteEmMarco!.Id.Should().Be(antiga.Id);
        vigenteEmMarco.CongelaConfiguracao.Should().BeFalse(
            "o que o catálogo dizia em março de 2026 não muda porque o tipo foi reeditado depois");
    }

    [Fact(DisplayName = "Antes do início da primeira versão não há tipo vigente")]
    public async Task ObterVigente_AntesDoInicio_DevolveNulo()
    {
        string codigo = CodigoUnico();
        await Gravar(Novo(codigo, Inicio, vigenciaFim: null));

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new TipoAtoPublicadoRepository(ctx, TimeProvider.System);

        (await repo.ObterVigenteAsync(codigo, Inicio.AddDays(-1), default)).Should().BeNull();
    }

    [Fact(DisplayName = "Remover marca a linha como excluída em vez de apagá-la")]
    public async Task Remover_MarcaComoExcluido()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado original = Novo(codigo, Inicio, vigenciaFim: null);
        await Gravar(original);
        await Excluir(original.Id);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);

        // Sem IgnoreQueryFilters a linha some — e sumir é indistinguível de ter sido
        // apagada. A afirmação de que o delete é lógico só se sustenta olhando a
        // linha por trás do filtro.
        TipoAtoPublicado excluido = await ctx.TiposAtoPublicado
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == original.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedAt.Should().NotBeNull();
        excluido.DeletedBy.Should().Be(Admin);

        (await ctx.TiposAtoPublicado.AnyAsync(t => t.Id == original.Id))
            .Should().BeFalse("a leitura normal oculta o registro excluído");
    }

    [Fact(DisplayName = "Soft-delete libera a janela para uma nova versão do mesmo código")]
    public async Task SoftDelete_LiberaJanela()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado original = Novo(codigo, Inicio, vigenciaFim: null);
        await Gravar(original);
        await Excluir(original.Id);

        TipoAtoPublicado recriado = Novo(codigo, Inicio, vigenciaFim: null);
        await Gravar(recriado);

        await using PublicacoesDbContext leitura = _fixture.CreateDbContext(userId: null);
        var repo = new TipoAtoPublicadoRepository(leitura, TimeProvider.System);

        (await repo.ObterVigenteAsync(codigo, Inicio, default))!.Id.Should().Be(recriado.Id);

        // A versão excluída continua no banco, com a mesma janela — a exclusion
        // constraint a ignora porque o predicado dela é `is_deleted = false`.
        (await leitura.TiposAtoPublicado.IgnoreQueryFilters()
            .CountAsync(t => t.Codigo == codigo)).Should().Be(2);
    }

    [Fact(DisplayName = "CHECK recusa janela vazia vinda de insert cru")]
    public async Task CheckVigencia_RecusaJanelaVazia()
    {
        // O guard de domínio já recusa; o CHECK é a defesa contra inserts que não
        // passam pelo agregado (seed, correção manual, ferramenta administrativa).
        Func<Task> insercao = () => InserirCru(CodigoUnico(), Inicio, Inicio);

        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa código fora do formato vindo de insert cru")]
    public async Task CheckFormato_RecusaCodigoInvalido()
    {
        Func<Task> insercao = () => InserirCru("edital abertura", Inicio, null);

        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "ExisteSobreposicaoDeVigencia detecta cruzamento e ignora a própria versão")]
    public async Task ExisteSobreposicao_DetectaCruzamentoEIgnoraProprio()
    {
        string codigo = CodigoUnico();
        TipoAtoPublicado existente = Novo(codigo, Inicio, Fim);
        await Gravar(existente);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new TipoAtoPublicadoRepository(ctx, TimeProvider.System);

        // Cruza.
        (await repo.ExisteSobreposicaoDeVigenciaAsync(codigo, Meio, null, null, default))
            .Should().BeTrue();

        // Adjacente pela direita: começa exatamente onde a outra termina.
        (await repo.ExisteSobreposicaoDeVigenciaAsync(codigo, Fim, null, null, default))
            .Should().BeFalse();

        // Adjacente pela esquerda: termina exatamente onde a outra começa.
        (await repo.ExisteSobreposicaoDeVigenciaAsync(codigo, Inicio.AddYears(-1), Inicio, null, default))
            .Should().BeFalse();

        // A própria versão não conta como sobreposição de si mesma.
        (await repo.ExisteSobreposicaoDeVigenciaAsync(codigo, Inicio, Fim, existente.Id, default))
            .Should().BeFalse();

        // Outro código, mesma janela.
        (await repo.ExisteSobreposicaoDeVigenciaAsync(CodigoUnico(), Inicio, Fim, null, default))
            .Should().BeFalse();
    }

    private async Task Gravar(TipoAtoPublicado tipo)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(Admin);
        ctx.TiposAtoPublicado.Add(tipo);
        await ctx.SaveChangesAsync();
    }

    private async Task Excluir(Guid id)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(Admin);
        TipoAtoPublicado alvo = await ctx.TiposAtoPublicado.SingleAsync(t => t.Id == id);
        ctx.TiposAtoPublicado.Remove(alvo);
        await ctx.SaveChangesAsync();
    }

    private async Task InserirCru(string codigo, DateOnly inicio, DateOnly? fim)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO publicacoes.tipo_ato_publicado
                (id, codigo, nome, congela_configuracao, unico_por_objeto, efeito_irreversivel,
                 vigencia_inicio, vigencia_fim, created_at, is_deleted)
            VALUES
                (gen_random_uuid(), @codigo, 'Insert cru', false, false, false,
                 @inicio, @fim, now(), false)
            """,
            (NpgsqlConnection)ctx.Database.GetDbConnection());

        command.Parameters.AddWithValue("codigo", codigo);
        command.Parameters.AddWithValue("inicio", inicio);
        command.Parameters.AddWithValue("fim", fim ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private static TipoAtoPublicado Novo(
        string codigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        bool congela = true,
        bool irreversivel = false) =>
        TipoAtoPublicado.Criar(
            codigo,
            nome: "Tipo de ato de teste",
            congelaConfiguracao: congela,
            unicoPorObjeto: false,
            efeitoIrreversivel: irreversivel,
            vigenciaInicio: vigenciaInicio,
            vigenciaFim: vigenciaFim,
            baseLegal: null).Value!;

    /// <summary>
    /// Código único no formato aceito pelo CHECK: os 12 primeiros dígitos hex do
    /// Guid mapeados para as letras A–P, o que evita colisão entre testes que
    /// compartilham o mesmo container.
    /// </summary>
    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "TESTE_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
