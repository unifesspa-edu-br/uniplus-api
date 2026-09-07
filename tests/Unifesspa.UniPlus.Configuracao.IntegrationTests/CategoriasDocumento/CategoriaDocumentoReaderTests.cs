namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O leitor cross-módulo de categoria de documento (ADR-0056), contra Postgres real.
/// Quem o consome é o Módulo Seleção, ao declarar o recorte de competência de uma banca
/// requerida por fase: é este leitor que decide se a categoria referenciada existe e
/// segue viva no cadastro, e uma categoria removida que continuasse respondendo faria o
/// edital congelar competência sobre um rótulo que saiu do catálogo.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CategoriaDocumentoReaderTests
{
    private const string AdminA = "admin-a";

    private readonly ConfiguracaoDbFixture _fixture;

    public CategoriaDocumentoReaderTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ObterPorIdAsync devolve a categoria viva com código, nome, descrição e ordem")]
    public async Task ObterPorIdAsync_CategoriaViva_DevolveCamposDoCadastro()
    {
        string codigo = CodigoUnico();
        CategoriaDocumento categoria = CategoriaDocumento.Criar(codigo, "Comprovação de renda", "Bloco de renda", 30).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CategoriasDocumento.Add(categoria);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new CategoriaDocumentoReader(readCtx);

        CategoriaDocumentoView? view = await reader.ObterPorIdAsync(categoria.Id);

        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
        view.Nome.Should().Be("Comprovação de renda");
        view.Descricao.Should().Be("Bloco de renda");
        view.Ordem.Should().Be(30);
    }

    [Fact(DisplayName = "ObterPorIdAsync não devolve categoria removida")]
    public async Task ObterPorIdAsync_CategoriaRemovida_DevolveNulo()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(CodigoUnico(), "Identificação", null, 0).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CategoriasDocumento.Add(categoria);
            await ctx.SaveChangesAsync();

            categoria.MarkAsDeleted(AdminA, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new CategoriaDocumentoReader(readCtx);

        CategoriaDocumentoView? view = await reader.ObterPorIdAsync(categoria.Id);

        view.Should().BeNull();
    }

    [Fact(DisplayName = "ListarVivosAsync ordena por código e exclui as removidas")]
    public async Task ListarVivosAsync_OrdenaPorCodigoEExcluiRemovidas()
    {
        string prefixo = $"ZZ{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        string codigoB = $"{prefixo}_B";
        string codigoA = $"{prefixo}_A";
        string codigoRemovido = $"{prefixo}_C";

        CategoriaDocumento removida = CategoriaDocumento.Criar(codigoRemovido, "Removida", null, 0).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            // Inseridas fora de ordem alfabética, e com Ordem que contraria o alfabeto: o
            // leitor precisa ordenar pelo código, não pela ordem de inserção nem pela
            // Ordem de exibição do cadastro, que admite empate.
            ctx.CategoriasDocumento.Add(CategoriaDocumento.Criar(codigoB, "Categoria Bê", null, 1).Value!);
            ctx.CategoriasDocumento.Add(CategoriaDocumento.Criar(codigoA, "Categoria Á", null, 99).Value!);
            ctx.CategoriasDocumento.Add(removida);
            await ctx.SaveChangesAsync();

            removida.MarkAsDeleted(AdminA, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new CategoriaDocumentoReader(readCtx);

        IReadOnlyList<CategoriaDocumentoView> todas = await reader.ListarVivosAsync();

        string[] minhas = [.. todas
            .Select(v => v.Codigo)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];

        minhas.Should().Equal([codigoA, codigoB]);
    }

    private static string CodigoUnico() => $"CAT_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
