namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Confere o catálogo semeado por <c>HasData</c> contra
/// <see cref="CategoriaDocumentoSeed"/> — a fonte única — item a item, e a ordem em
/// que o repositório devolve o catálogo. Um seed que não bate com a lista significa
/// migration desatualizada: o banco de qualquer ambiente novo divergiria do que o
/// código diz semear. As invariantes da própria lista (catálogo aprovado, formato
/// dos códigos, unicidade) são guardadas sem banco em
/// <c>CatalogoDeCategoriasDocumentoTests</c>.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CategoriaDocumentoSeedTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public CategoriaDocumentoSeedTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "As dez categorias do seed existem no banco migrado, com id, código, nome e ordem da fonte única")]
    public async Task Seed_MaterializadoNoBanco_BateComAFonteUnica()
    {
        Guid[] idsDoSeed = [.. CategoriaDocumentoSeed.Itens.Select(item => item.Id)];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        Dictionary<Guid, CategoriaDocumento> persistidas = await ctx.CategoriasDocumento
            .AsNoTracking()
            .Where(c => idsDoSeed.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        persistidas.Should().HaveCount(CategoriaDocumentoSeed.Itens.Count);

        foreach (CategoriaDocumentoSeedItem item in CategoriaDocumentoSeed.Itens)
        {
            persistidas.Should().ContainKey(item.Id, "o identificador do seed é fixo e igual em todo ambiente");
            CategoriaDocumento persistida = persistidas[item.Id];
            persistida.Codigo.Valor.Should().Be(item.Codigo);
            persistida.Nome.Should().Be(item.Nome);
            persistida.Ordem.Should().Be(item.Ordem);
            persistida.IsDeleted.Should().BeFalse();
        }
    }

    [Fact(DisplayName = "A ordem de exibição vence a ordem de criação, e o código desempata a ordem repetida")]
    public async Task ListarVivasOrdenadas_OrdenaPorOrdemDepoisCodigo_NaoPorId()
    {
        // Os identificadores são Guid v7: criados nesta sequência, crescem no tempo.
        // Como a ordem de exibição atribuída é a inversa, uma listagem que ordenasse
        // por Id devolveria exatamente o contrário do esperado. As duas últimas
        // empatam em ordem para exercitar o desempate pelo código.
        string prefixo = $"CATORD{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        CategoriaDocumento ultima = Nova($"{prefixo}_D", ordem: 900);
        CategoriaDocumento empateB = Nova($"{prefixo}_B", ordem: 800);
        CategoriaDocumento empateA = Nova($"{prefixo}_A", ordem: 800);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-ordem"))
        {
            ctx.CategoriasDocumento.Add(ultima);
            ctx.CategoriasDocumento.Add(empateB);
            ctx.CategoriasDocumento.Add(empateA);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var repositorio = new CategoriaDocumentoRepository(readCtx);

        IReadOnlyList<CategoriaDocumento> categorias = await repositorio.ListarVivasOrdenadasAsync(CancellationToken.None);

        string[] meus = [.. categorias
            .Select(c => c.Codigo.Valor)
            .Where(codigo => codigo.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([$"{prefixo}_A", $"{prefixo}_B", $"{prefixo}_D"],
            "empate de ordem desempata pelo código, e a ordem 900 vem depois das 800 — ainda que o registro de ordem 900 tenha sido criado primeiro");
    }

    [Fact(DisplayName = "O catálogo semeado sai do repositório na ordem de exibição declarada")]
    public async Task ListarVivasOrdenadas_DevolveOSeedNaOrdemDeclarada()
    {
        string[] esperados = [.. CategoriaDocumentoSeed.Itens.OrderBy(i => i.Ordem).Select(i => i.Codigo)];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repositorio = new CategoriaDocumentoRepository(ctx);

        IReadOnlyList<CategoriaDocumento> categorias = await repositorio.ListarVivasOrdenadasAsync(CancellationToken.None);

        string[] doSeed = [.. categorias
            .Select(c => c.Codigo.Valor)
            .Where(codigo => esperados.Contains(codigo))];

        doSeed.Should().Equal(esperados);
    }

    private static CategoriaDocumento Nova(string codigo, int ordem) =>
        CategoriaDocumento.Criar(codigo, "Categoria de teste de ordenação", null, ordem).Value!;
}
