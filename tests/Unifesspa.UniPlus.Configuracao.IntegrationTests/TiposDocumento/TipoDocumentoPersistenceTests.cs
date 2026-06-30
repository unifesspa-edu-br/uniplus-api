namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do TipoDocumento contra Postgres real (UNI-REQ-0013):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECKs de domínio (categoria) e de auto-equivalência, não-bloqueio de remoção de
/// um tipo apontado como equivalente, e leitura cross-módulo (CA-01, CA-02, CA-04).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDocumentoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoDocumentoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string codigo = CodigoUnico();
        TipoDocumento tipo = Novo(codigo, categoria: "SAUDE");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDocumento persistido = await readCtx.TiposDocumento.SingleAsync(t => t.Id == tipo.Id);

        persistido.Codigo.Should().Be(codigo);
        persistido.Nome.Should().Be("Laudo médico");
        persistido.Categoria.Should().Be(Domain.Enums.CategoriaDocumento.Saude);
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();

        var reader = new TipoDocumentoReader(readCtx);
        TipoDocumentoView? view = await reader.ObterPorIdAsync(tipo.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
        view.Categoria.Should().Be("SAUDE");
    }

    [Fact(DisplayName = "CA-02: UNIQUE parcial do código rejeita segundo tipo vivo com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(Novo(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.TiposDocumento.Add(Novo(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCodigoConflict) em
        // CodigoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_tipo_documento_codigo_vivo");
    }

    [Fact(DisplayName = "Código distinto é aceito")]
    public async Task CodigoDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.TiposDocumento.Add(Novo(CodigoUnico()));
        ctx.TiposDocumento.Add(Novo(CodigoUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os códigos são distintos");
    }

    [Fact(DisplayName = "CA-04: soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string codigo = CodigoUnico();
        TipoDocumento tipo = Novo(codigo);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoDocumento tracked = await ctx.TiposDocumento.SingleAsync(t => t.Id == tipo.Id);
            ctx.TiposDocumento.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            TipoDocumento excluido = await ctx.TiposDocumento
                .IgnoreQueryFilters().SingleAsync(t => t.Id == tipo.Id);
            excluido.IsDeleted.Should().BeTrue();
            excluido.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.TiposDocumento.Add(Novo(codigo));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CA-04: remover um tipo apontado como equivalente por outro vivo NÃO é bloqueado")]
    public async Task SoftDelete_TipoApontadoComoEquivalente_NaoBloqueia()
    {
        string codigoCin = CodigoUnico();
        string codigoRg = CodigoUnico();

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            // CIN existe; RG aponta CIN como equivalente (rótulo classificatório, sem FK).
            ctx.TiposDocumento.Add(Novo(codigoCin, categoria: "IDENTIFICACAO"));
            ctx.TiposDocumento.Add(Novo(codigoRg, categoria: "IDENTIFICACAO", tipoEquivalente: codigoCin));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoDocumento cin = await ctx.TiposDocumento.SingleAsync(t => t.Codigo == codigoCin);
            ctx.TiposDocumento.Remove(cin);
            Func<Task> act = async () => await ctx.SaveChangesAsync();
            await act.Should().NotThrowAsync("tipo_equivalente é rótulo, não FK — a remoção não é bloqueada");
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDocumento rg = await readCtx.TiposDocumento.SingleAsync(t => t.Codigo == codigoRg);
        rg.TipoEquivalente.Should().Be(codigoCin, "o rótulo permanece, agora apontando para um código sem alvo vivo");
    }

    [Fact(DisplayName = "CHECK de banco rejeita categoria fora do domínio via SQL cru")]
    public async Task Check_RejeitaCategoriaForaDoDominioViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"FINANCEIRO"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de categoria impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita tipo_equivalente igual ao código via SQL cru")]
    public async Task Check_RejeitaEquivalenteIgualCodigoViaSqlCru()
    {
        string codigo = CodigoUnico();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, tipo_equivalente, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {codigo}, {"X"}, {"OUTROS"}, {codigo}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK tipo_equivalente <> codigo impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita tamanho_maximo_mb não-positivo via SQL cru")]
    public async Task Check_RejeitaTamanhoMaximoNaoPositivoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, tamanho_maximo_mb, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"OUTROS"}, {0}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK tamanho_maximo_mb > 0 impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por código e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        string prefixo = $"DOC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string codA = $"{prefixo}_A";
        string codB = $"{prefixo}_B";
        string codExcluido = $"{prefixo}_D";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(Novo(codB));
            ctx.TiposDocumento.Add(Novo(codA));
            ctx.TiposDocumento.Add(Novo(codExcluido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoDocumento aExcluir = await ctx.TiposDocumento.SingleAsync(t => t.Codigo == codExcluido);
            ctx.TiposDocumento.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoDocumentoReader(readCtx);
        IReadOnlyList<TipoDocumentoView> todos = await reader.ListarVivosAsync();

        string[] meus = [.. todos
            .Select(v => v.Codigo)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([codA, codB]);
    }

    private static TipoDocumento Novo(
        string codigo,
        string categoria = "SAUDE",
        string? tipoEquivalente = null) =>
        TipoDocumento.Criar(codigo, "Laudo médico", null, categoria, "pdf,jpg", 10, tipoEquivalente).Value!;

    private static string CodigoUnico() => $"DOC_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
