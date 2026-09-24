namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// A origem da nota no ENEM contra Postgres real: a carga do cadastro a liga só no tipo de
/// nota do ENEM, a leitura cross-módulo a expõe, e o banco recusa esse tipo sem pontuação.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoEtapaPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public TipoEtapaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A carga do cadastro liga a nota de origem no ENEM só no tipo NOTA_ENEM, e a leitura cross-módulo a expõe")]
    public async Task Carga_LigaANotaDeOrigemNoEnemSoNoTipoDoEnem()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        IReadOnlyList<TipoEtapaView> ativos = await new TipoEtapaReader(ctx).ListarAtivosAsync(CancellationToken.None);

        ativos.Where(tipo => tipo.NotaDeOrigemNoEnem).Select(tipo => tipo.Codigo).Should().Equal("NOTA_ENEM");
    }

    [Fact(DisplayName = "O banco recusa tirar a pontuação do tipo com nota de origem no ENEM")]
    public async Task CheckConstraint_NotaDeOrigemNoEnemSemPontuacao_Recusa()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        // A linha semeada é compartilhada pela coleção: a transação desfeita no fim a preserva
        // mesmo que o CHECK deixe de recusar.
        await using IDbContextTransaction transacao =
            await ctx.Database.BeginTransactionAsync();

        // Bypassa o domínio de propósito: o CHECK é defesa em profundidade contra escrita fora
        // do fluxo da aplicação.
        Func<Task> act = async () => await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE configuracao.tipos_etapa SET admite_pontuacao = false WHERE codigo = 'NOTA_ENEM'");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        await transacao.RollbackAsync();
        pg.SqlState.Should().Be("23514", "violação de CHECK constraint");
        pg.ConstraintName.Should().Be("ck_tipos_etapa_nota_de_origem_no_enem_admite_pontuacao");
    }
}
