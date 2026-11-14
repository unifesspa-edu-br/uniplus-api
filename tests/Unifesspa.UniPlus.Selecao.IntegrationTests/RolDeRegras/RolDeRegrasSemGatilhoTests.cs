namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O append-only do <c>rol_de_regras</c> é por referência congelada, não por gatilho (ADR-0112): a base subida pelo
/// caminho de produção, com a migration e os scripts, não pode ter gatilho sobre a tabela.
/// </summary>
public sealed class RolDeRegrasSemGatilhoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RolDeRegrasSemGatilhoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Nenhum gatilho incide sobre o rol de regras depois da subida da base")]
    public async Task Catalogo_RolDeRegras_NaoTemGatilho()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        List<string> gatilhos = await context.Database
            .SqlQuery<string>($"""
                SELECT pg_get_triggerdef(t.oid) AS "Value"
                FROM pg_trigger t
                WHERE t.tgrelid = 'selecao.rol_de_regras'::regclass AND NOT t.tgisinternal
                """)
            .ToListAsync();

        gatilhos.Should().BeEmpty("o append-only do rol é por referência congelada, não por gatilho (ADR-0112)");
    }
}
