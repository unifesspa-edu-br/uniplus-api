namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposBanca;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Integração ponta-a-ponta do TipoBanca contra Postgres real (UNI-REQ-0139):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECK do vocabulário de tipos de banca e leitura cross-módulo. Cada teste usa um tipo
/// distinto (o vocabulário é fechado).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoBancaPersistenceTests : IAsyncLifetime
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoBancaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => TiposDeBancaDeTeste.ApagarAsync(_fixture.ConnectionString);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        TipoBanca banca = Banca("BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental", "Homologação");
        await GravarAsync(banca, AdminA);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoBanca persistida = await readCtx.TiposBanca.SingleAsync(b => b.Id == banca.Id);
        DeveTerPersistidoOsCampos(persistida);

        TipoBancaView? view = await new TipoBancaReader(readCtx).ObterPorIdAsync(banca.Id);
        DeveEstarVisivelPeloLeitor(view);
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda banca viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        await GravarAsync(Banca("BANCA_ENTREVISTA", "Banca de entrevista", null), AdminA);

        Func<Task> act = () => GravarAsync(Banca("BANCA_ENTREVISTA", "Banca de entrevista", null), AdminA);

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.TipoDeBancaCodigoAtivos);
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_LiberaSlot()
    {
        TipoBanca banca = Banca("BANCA_CORRECAO_REDACOES", "Banca de correção de redações", null);
        await GravarAsync(banca, AdminA);
        await ExcluirAsync(banca.Id, AdminB);
        await DeveTerGuardadoATrilhaDaExclusaoAsync(banca.Id);

        Func<Task> act = () => GravarAsync(Banca("BANCA_CORRECAO_REDACOES", "Banca de correção de redações", null), AdminA);

        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do conjunto canônico via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoCanonicoViaSqlCru()
    {
        Func<Task> act = () => InserirViaSqlCruAsync("BANCA_LOGISTICA", "AVALIA_O_MERITO", ["NOTA_DA_ETAPA"]);

        (await act.Should().ThrowAsync<Npgsql.PostgresException>("o CHECK de conjunto canônico impede o INSERT direto"))
            .Which.ConstraintName.Should().Be("ck_tipos_de_banca_codigo_canonico");
    }

    [Fact(DisplayName = "CHECK de banco rejeita papel fora do vocabulário via SQL cru")]
    public async Task Check_RejeitaPapelForaDoVocabularioViaSqlCru()
    {
        Func<Task> act = () => InserirViaSqlCruAsync("BANCA_ENTREVISTA", "PAPEL_QUE_NAO_EXISTE", []);

        (await act.Should().ThrowAsync<Npgsql.PostgresException>())
            .Which.ConstraintName.Should().Be("ck_tipos_de_banca_papel");
    }

    [Fact(DisplayName = "CHECK de banco aceita o tipo de 2ª instância com o papel que julga o recurso em 2ª instância, sem tipos de decisão")]
    public async Task Check_AceitaOTipoEOPapelDaSegundaInstanciaViaSqlCru()
    {
        await InserirViaSqlCruAsync("BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA", "JULGA_A_SEGUNDA_INSTANCIA", []);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        TipoBanca gravado = await leitura.TiposBanca.SingleAsync(b => b.Codigo == TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia);
        gravado.Atuacao.Papel.Should().Be(PapelDaBanca.JulgaASegundaInstancia);
        gravado.Atuacao.TiposDeDecisaoJulgados.Should().BeEmpty();
    }

    [Fact(DisplayName = "CHECK de banco rejeita tipo de decisão fora do vocabulário via SQL cru")]
    public async Task Check_RejeitaTipoDeDecisaoForaDoVocabularioViaSqlCru()
    {
        Func<Task> act = () => InserirViaSqlCruAsync("BANCA_ENTREVISTA", "AVALIA_O_MERITO", ["NOTA_DA_ETAPA", "TIPO_QUE_NAO_EXISTE"]);

        (await act.Should().ThrowAsync<Npgsql.PostgresException>())
            .Which.ConstraintName.Should().Be("ck_tipos_de_banca_decisoes");
    }

    [Fact(DisplayName = "Papel, as duas marcas verdadeiras e tipos de decisão são gravados pelos códigos de contrato e relidos elemento a elemento")]
    public async Task Insert_Atuacao_GravaOsCodigosELeOsElementos()
    {
        TipoBanca banca = TipoBanca.Criar("BANCA_ANALISE_DOCUMENTAL", new DeclaracaoDoTipoDeBanca(
            "Banca de análise documental", Papel: "AVALIA_O_MERITO", ExigeDuasOuMaisBancas: true, EncerraRecursoNaPrimeiraInstancia: true,
            TiposDeDecisaoJulgados: ["ISENCAO_DA_TAXA", "ANALISE_DOCUMENTAL"])).Value!;
        await GravarAsync(banca, AdminA);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);

        (await TextoGravadoDaAtuacaoAsync(leitura, banca.Id)).Should().Be("AVALIA_O_MERITO|true|true|ANALISE_DOCUMENTAL,ISENCAO_DA_TAXA");
        (await leitura.TiposBanca.SingleAsync(b => b.Id == banca.Id)).Atuacao.Papel.Should().Be(PapelDaBanca.AvaliaOMerito);
        DeveLerAAtuacaoDaView((await new TipoBancaReader(leitura).ObterPorIdAsync(banca.Id))!);
    }

    [Fact(DisplayName = "Trocar a lista de tipos de decisão no lugar, esvaziando e repreenchendo, é detectado e gravado")]
    public async Task Update_TrocaDaListaNoLugar_EGravada()
    {
        TipoBanca banca = TipoBanca.Criar("BANCA_ENTREVISTA", new DeclaracaoDoTipoDeBanca(
            "Banca de entrevista", Papel: "AVALIA_O_MERITO", TiposDeDecisaoJulgados: ["NOTA_DA_ETAPA"])).Value!;
        await GravarAsync(banca, AdminA);

        await TrocarOsTiposDeDecisaoAsync(banca.Id, ["CLASSIFICACAO", "NOTA_DA_ETAPA"]);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        (await leitura.TiposBanca.SingleAsync(b => b.Id == banca.Id)).Atuacao.TiposDeDecisaoJulgados
            .Should().Equal(TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.NotaDaEtapa);
    }

    private static Task<string> TextoGravadoDaAtuacaoAsync(UniPlusDbContext leitura, Guid id) =>
        leitura.Database
            .SqlQuery<string>($"SELECT papel || '|' || exige_duas_ou_mais_bancas || '|' || encerra_recurso_na_primeira_instancia || '|' || array_to_string(tipos_de_decisao_julgados, ',') AS \"Value\" FROM configuracao.tipos_de_banca WHERE id = {id}")
            .SingleAsync();

    private static void DeveLerAAtuacaoDaView(TipoBancaView view)
    {
        view.Papel.Should().Be(PapelDaBanca.AvaliaOMerito);
        view.ExigeDuasOuMaisBancas.Should().BeTrue();
        view.EncerraRecursoNaPrimeiraInstancia.Should().BeTrue();
        view.TiposDeDecisaoJulgados.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.IsencaoDaTaxa);
    }

    private async Task TrocarOsTiposDeDecisaoAsync(Guid id, string[] tipos)
    {
        await using UniPlusDbContext edicao = _fixture.CreateDbContext(AdminB);
        TipoBanca carregada = await edicao.TiposBanca.SingleAsync(b => b.Id == id);
        carregada.Atualizar(new DeclaracaoDoTipoDeBanca(
            "Banca de entrevista", Papel: "AVALIA_O_MERITO", TiposDeDecisaoJulgados: tipos)).IsSuccess.Should().BeTrue();
        await edicao.SaveChangesAsync();
    }

    [Theory(DisplayName = "Os tipos de banca acrescentados ao vocabulário são gravados e lidos pelo código de contrato")]
    [InlineData("BANCA_HOMOLOGACAO_INSCRICOES", TipoDeBanca.HomologacaoDasInscricoes)]
    [InlineData("BANCA_IMPORTACAO_NOTAS_INEP", TipoDeBanca.ImportacaoDasNotasDoInep)]
    [InlineData("BANCA_IMPORTACAO_CANDIDATOS", TipoDeBanca.ImportacaoDeCandidatos)]
    [InlineData("BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA", TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia)]
    public async Task Insert_TipoAcrescentadoAoVocabulario_GravaOCodigoELeOTipo(string codigo, TipoDeBanca esperado)
    {
        TipoBanca banca = Banca(codigo, $"Banca {codigo}", null);
        await GravarAsync(banca, AdminA);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        string gravado = await leitura.Database
            .SqlQuery<string>($"SELECT codigo AS \"Value\" FROM configuracao.tipos_de_banca WHERE id = {banca.Id}")
            .SingleAsync();

        gravado.Should().Be(codigo);
        (await leitura.TiposBanca.SingleAsync(b => b.Id == banca.Id)).Codigo.Should().Be(esperado);
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync exclui bancas soft-deleted")]
    public async Task ListarVivos_ExcluiSoftDeleted()
    {
        TipoBanca banca = Banca("BANCA_ANALISE_RECURSOS", "Banca de análise de recursos", null);
        await GravarAsync(banca, AdminA);
        await ExcluirAsync(banca.Id, AdminB);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        IReadOnlyList<TipoBancaView> todos = await new TipoBancaReader(readCtx).ListarVivosAsync();

        string[] codigos = [.. todos.Select(v => v.Codigo.Codigo())];
        codigos.Should().BeInAscendingOrder("o leitor ordena por código ascendente");
        codigos.Should().NotContain("BANCA_ANALISE_RECURSOS", "a banca soft-deleted não aparece no leitor de vivos");
    }

    private async Task InserirViaSqlCruAsync(string codigo, string papel, string[] tipos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO configuracao.tipos_de_banca (id, codigo, nome, papel, exige_duas_ou_mais_bancas, encerra_recurso_na_primeira_instancia, tipos_de_decisao_julgados, created_at, is_deleted) VALUES (@id, @codigo, @nome, @papel, false, false, @tipos, @criado, false)",
            new NpgsqlParameter("id", Guid.CreateVersion7()),
            new NpgsqlParameter("codigo", codigo),
            new NpgsqlParameter("nome", "Banca"),
            new NpgsqlParameter("papel", papel),
            new NpgsqlParameter("tipos", tipos),
            new NpgsqlParameter("criado", DateTimeOffset.UtcNow));
    }

    private async Task GravarAsync(TipoBanca banca, string usuario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.TiposBanca.Add(banca);
        await ctx.SaveChangesAsync();
    }

    private async Task ExcluirAsync(Guid id, string usuario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        TipoBanca aExcluir = await ctx.TiposBanca.SingleAsync(b => b.Id == id);
        ctx.TiposBanca.Remove(aExcluir);
        await ctx.SaveChangesAsync();
    }

    private async Task DeveTerGuardadoATrilhaDaExclusaoAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        TipoBanca excluida = await ctx.TiposBanca.IgnoreQueryFilters().SingleAsync(b => b.Id == id);

        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    private static void DeveTerPersistidoOsCampos(TipoBanca persistida)
    {
        persistida.Codigo.Should().Be(TipoDeBanca.AnaliseDocumental);
        persistida.FaseTipica.Should().Be("Homologação");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
    }

    private static void DeveEstarVisivelPeloLeitor(TipoBancaView? view)
    {
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(TipoDeBanca.AnaliseDocumental);
        view.FaseTipica.Should().Be("Homologação");
    }

    private static TipoBanca Banca(string codigo, string nome, string? faseTipica) =>
        TipoBanca.Criar(codigo, new DeclaracaoDoTipoDeBanca(nome, faseTipica, Papel: "IMPORTA_CANDIDATOS")).Value!;
}
