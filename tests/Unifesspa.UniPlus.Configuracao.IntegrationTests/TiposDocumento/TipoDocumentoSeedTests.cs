namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Confere o catálogo semeado por <c>HasData</c> contra
/// <see cref="TipoDocumentoSeed"/> — a fonte única — item a item. Um seed que não
/// bate com a lista significa migration desatualizada: o banco de qualquer
/// ambiente novo divergiria do que o código diz semear. As invariantes da própria
/// lista — formato dos códigos, unicidade, categoria existente — são guardadas sem
/// banco em <c>CatalogoDeTiposDocumentoTests</c>.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDocumentoSeedTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public TipoDocumentoSeedTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os setenta tipos do seed existem no banco migrado, com id, código, nome e categoria da fonte única")]
    public async Task Seed_MaterializadoNoBanco_BateComAFonteUnica()
    {
        Guid[] idsDoSeed = [.. TipoDocumentoSeed.Itens.Select(item => item.Id)];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        Dictionary<Guid, TipoDocumento> persistidos = await ctx.TiposDocumento
            .AsNoTracking()
            .Where(t => idsDoSeed.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        persistidos.Should().HaveCount(TipoDocumentoSeed.Itens.Count);

        foreach (TipoDocumentoSeedItem item in TipoDocumentoSeed.Itens)
        {
            persistidos.Should().ContainKey(item.Id, "o identificador do seed é fixo e igual em todo ambiente");
            TipoDocumento persistido = persistidos[item.Id];
            persistido.Codigo.Valor.Should().Be(item.Codigo);
            persistido.Nome.Should().Be(item.Nome);
            persistido.Categoria.Should().Be(item.Categoria);
            persistido.IsDeleted.Should().BeFalse();
        }
    }
}
