namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os anexos do ato contra Postgres real: persistem na ordem enviada e, como o ato,
/// só aceitam inserção — o trigger forense cobre a tabela.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AnexoDoAtoPersistenceTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OutroHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private const string RestrictViolation = "23001";
    private const string UniqueViolation = "23505";
    private static readonly AtoNormativoDeTeste Base = new() { Numero = null };

    private readonly PublicacoesDbFixture _fixture;

    public AnexoDoAtoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O ato persiste com os anexos, e a leitura os devolve na ordem enviada")]
    public async Task Insert_PersisteAnexosNaOrdemEnviada()
    {
        AtoNormativo ato = (Base with
        {
            Anexos = [("Anexo II.pdf", "application/pdf", HashValido), ("Anexo I.pdf", "application/pdf", OutroHash)],
        }).Registrar();
        await Gravar(ato);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo lido = await ctx.AtosNormativos.Include(a => a.Anexos).SingleAsync(a => a.Id == ato.Id);

        lido.Anexos.OrderBy(a => a.Ordem).Select(a => (a.Nome, a.TipoMidia, a.Hash))
            .Should().Equal(
                ("Anexo II.pdf", "application/pdf", HashValido),
                ("Anexo I.pdf", "application/pdf", OutroHash));
    }

    [Fact(DisplayName = "UPDATE cru no anexo é bloqueado pelo trigger append-only")]
    public async Task Update_Anexo_Bloqueado()
    {
        AtoNormativo ato = (Base with { Anexos = [("Anexo I.pdf", "application/pdf", HashValido)] }).Registrar();
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"UPDATE publicacoes.anexos_do_ato SET hash = {OutroHash} WHERE ato_id = {ato.Id}"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "DELETE cru no anexo é bloqueado pelo trigger append-only")]
    public async Task Delete_Anexo_Bloqueado()
    {
        AtoNormativo ato = (Base with { Anexos = [("Anexo I.pdf", "application/pdf", HashValido)] }).Registrar();
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"DELETE FROM publicacoes.anexos_do_ato WHERE ato_id = {ato.Id}"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "TRUNCATE do anexo é bloqueado — não é DELETE, e esvaziaria o registro sem disparar o append-only")]
    public async Task Truncate_DoAnexo_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"TRUNCATE publicacoes.anexos_do_ato"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "INSERT cru de anexo em ato gravado em transação anterior é recusado")]
    public async Task Insert_AnexoEmAtoDeTransacaoAnterior_Recusado()
    {
        AtoNormativo ato = Base.Registrar();
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.anexos_do_ato (id, ato_id, ordem, nome, tipo_midia, hash)
            VALUES ({Guid.CreateVersion7()}, {ato.Id}, 0, 'Tardio.pdf', 'application/pdf', {OutroHash})
            """));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "INSERT cru de anexo na mesma transação que inseriu o ato passa")]
    public async Task Insert_AnexoNaTransacaoDoAto_Passa()
    {
        AtoNormativo ato = Base.Registrar();

        await ExecutarNumaTransacaoAsync(InsertDoAtoCru(ato.Id), InsertDoAnexoCru(ato.Id));

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.AnexosDoAto.CountAsync(a => a.AtoId == ato.Id)).Should().Be(1);
    }

    [Fact(DisplayName = "Dois anexos com o mesmo nome no mesmo ato colidem no índice único")]
    public async Task Insert_NomeRepetido_Colide()
    {
        AtoNormativo ato = (Base with { Anexos = [("Anexo I.pdf", "application/pdf", HashValido)] }).Registrar();
        AnexoDoAto repetido = AnexoDoAto.Criar(ato, 1, "Anexo I.pdf", "application/pdf", OutroHash);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        ctx.AnexosDoAto.Add(repetido);

        DbUpdateException erro = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());

        ((PostgresException)erro.InnerException!).SqlState.Should().Be(UniqueViolation);
    }

    [Fact(DisplayName = "xmin de época anterior (que reconstruído cairia no futuro) não é transação em andamento, e não lança")]
    public async Task XidEmAndamento_XminNoFuturo_DevolveFalso()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT publicacoes.fn_xid_em_andamento('4294967290'::xid)";

        ((bool)(await comando.ExecuteScalarAsync())!).Should().BeFalse();
    }

    private async Task Gravar(AtoNormativo ato)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();
    }

    private static FormattableString InsertDoAtoCru(Guid atoId) =>
        $"""
        INSERT INTO publicacoes.atos_normativos
            (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
             unico_por_objeto, data_do_ato, documento_hash, assinante, publicado_em)
        VALUES ({atoId}, 'CEPS', 'EDITAL', 2026, NULL, 'EDITAL_ABERTURA', false, false, false,
                DATE '2026-03-13', {HashValido}, {PersonasDeTeste.NomeKevinCarvalho}, now())
        """;

    private static FormattableString InsertDoAnexoCru(Guid atoId) =>
        $"""
        INSERT INTO publicacoes.anexos_do_ato (id, ato_id, ordem, nome, tipo_midia, hash)
        VALUES ({Guid.CreateVersion7()}, {atoId}, 0, 'Anexo I.pdf', 'application/pdf', {OutroHash})
        """;

    private async Task ExecutarCruAsync(FormattableString sql)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(sql);
    }

    private async Task ExecutarNumaTransacaoAsync(params FormattableString[] comandos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();

        foreach (FormattableString sql in comandos)
        {
            await ctx.Database.ExecuteSqlAsync(sql);
        }

        await transacao.CommitAsync();
    }
}
