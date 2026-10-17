namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Os anexos do ato contra Postgres real: persistem na ordem enviada e, como o ato,
/// só aceitam inserção — o trigger forense cobre a tabela.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL cru escrito no próprio teste, com identificadores gerados nele — é o que se quer exercitar: a trava do banco contra escrita fora do agregado.")]
public sealed class AnexoDoAtoPersistenceTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OutroHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private const string RestrictViolation = "23001";
    private const string UniqueViolation = "23505";

    private readonly PublicacoesDbFixture _fixture;

    public AnexoDoAtoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O ato persiste com os anexos, e a leitura os devolve na ordem enviada")]
    public async Task Insert_PersisteAnexosNaOrdemEnviada()
    {
        AtoNormativo ato = Novo(
            ("Anexo II.pdf", "application/pdf", HashValido),
            ("Anexo I.pdf", "application/pdf", OutroHash));
        await Gravar(ato);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo lido = await ctx.AtosNormativos.Include(a => a.Anexos).SingleAsync(a => a.Id == ato.Id);

        lido.Anexos.OrderBy(a => a.Ordem).Select(a => (a.Nome, a.TipoMidia, a.Hash))
            .Should().Equal(
                ("Anexo II.pdf", "application/pdf", HashValido),
                ("Anexo I.pdf", "application/pdf", OutroHash));
    }

    [Fact(DisplayName = "UPDATE cru no anexo é bloqueado pelo trigger append-only")]
    public async Task Update_Anexo_Bloqueado()
    {
        AtoNormativo ato = Novo(("Anexo I.pdf", "application/pdf", HashValido));
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"UPDATE publicacoes.anexo_do_ato SET hash = '{OutroHash}' WHERE ato_id = '{ato.Id}'"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "DELETE cru no anexo é bloqueado pelo trigger append-only")]
    public async Task Delete_Anexo_Bloqueado()
    {
        AtoNormativo ato = Novo(("Anexo I.pdf", "application/pdf", HashValido));
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"DELETE FROM publicacoes.anexo_do_ato WHERE ato_id = '{ato.Id}'"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "TRUNCATE do anexo é bloqueado — não é DELETE, e esvaziaria o registro sem disparar o append-only")]
    public async Task Truncate_DoAnexo_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            "TRUNCATE publicacoes.anexo_do_ato"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "INSERT cru de dois anexos com o mesmo nome no ato colide no índice único")]
    public async Task Insert_NomeRepetido_Colide()
    {
        AtoNormativo ato = Novo(("Anexo I.pdf", "application/pdf", HashValido));
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.anexo_do_ato (id, ato_id, ordem, nome, tipo_midia, hash)
            VALUES ('{Guid.CreateVersion7()}', '{ato.Id}', 1, 'Anexo I.pdf', 'application/pdf', '{OutroHash}')
            """));

        erro.SqlState.Should().Be(UniqueViolation);
    }

    private async Task Gravar(AtoNormativo ato)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();
    }

    private async Task ExecutarCruAsync(string sql)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static AtoNormativo Novo(params (string Nome, string TipoMidia, string Hash)[] anexos) =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            orgao: "CEPS",
            serie: "EDITAL",
            ano: 2026,
            numero: null,
            tipoCodigo: "EDITAL_ABERTURA",
            congelaConfiguracao: false,
            efeitoIrreversivel: false,
            unicoPorObjeto: false,
            dataPublicacao: new DateOnly(2026, 3, 13),
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero),
            versaoInvocada: null,
            anexos: anexos);
}
