namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposBanca;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

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
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoBancaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoBancaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

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
        pg.ConstraintName.Should().Be(RestricoesDoBanco.TipoDeBancaCodigoVivo);
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
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipos_de_banca (id, codigo, nome, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"BANCA_LOGISTICA"}, {"Banca"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de conjunto canônico impede o INSERT direto");
    }

    [Theory(DisplayName = "Os tipos de banca acrescentados ao vocabulário são gravados e lidos pelo código de contrato")]
    [InlineData("BANCA_HOMOLOGACAO_INSCRICOES", TipoDeBanca.HomologacaoDasInscricoes)]
    [InlineData("BANCA_IMPORTACAO_NOTAS_INEP", TipoDeBanca.ImportacaoDasNotasDoInep)]
    [InlineData("BANCA_IMPORTACAO_CANDIDATOS", TipoDeBanca.ImportacaoDeCandidatos)]
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
        TipoBanca.Criar(codigo, nome, faseTipica, null).Value!;
}
