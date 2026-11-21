namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O leitor cross-módulo de categoria de documento (ADR-0056), contra Postgres real.
/// Quem o consome é o Módulo Seleção, ao declarar os candidatos atendidos por uma banca
/// requerida por fase: é este leitor que decide se a categoria referenciada existe e
/// segue viva no cadastro, e uma categoria removida que continuasse respondendo faria o
/// edital congelar competência sobre um rótulo que saiu do catálogo.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
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
        await CadastrarAsync(categoria);

        CategoriaDocumentoView? view = await ObterPorIdAsync(categoria.Id);

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
        await CadastrarEntaoRemoverAsync(categoria);

        CategoriaDocumentoView? view = await ObterPorIdAsync(categoria.Id);

        view.Should().BeNull();
    }

    [Fact(DisplayName = "ListarVivosAsync ordena por código e exclui as removidas")]
    public async Task ListarVivosAsync_OrdenaPorCodigoEExcluiRemovidas()
    {
        string prefixo = $"ZZ{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        CategoriaDocumento removida = CategoriaDocumento.Criar($"{prefixo}_C", "Removida", null, 0).Value!;

        // Inseridas fora de ordem alfabética, e com Ordem que contraria o alfabeto: o
        // leitor precisa ordenar pelo código, não pela ordem de inserção nem pela
        // Ordem de exibição do cadastro, que admite empate.
        await CadastrarEntaoRemoverAsync(
            removida,
            CategoriaDocumento.Criar($"{prefixo}_B", "Categoria Bê", null, 1).Value!,
            CategoriaDocumento.Criar($"{prefixo}_A", "Categoria Á", null, 99).Value!);

        string[] minhas = await CodigosVivosComPrefixoAsync(prefixo);

        minhas.Should().Equal([$"{prefixo}_A", $"{prefixo}_B"]);
    }

    private async Task CadastrarAsync(CategoriaDocumento categoria)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.CategoriasDocumento.Add(categoria);
        await ctx.SaveChangesAsync();
    }

    private async Task CadastrarEntaoRemoverAsync(CategoriaDocumento removida, params CategoriaDocumento[] vivas)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.CategoriasDocumento.AddRange(vivas);
        ctx.CategoriasDocumento.Add(removida);
        await ctx.SaveChangesAsync();

        removida.MarkAsDeleted(AdminA, DateTimeOffset.UtcNow);
        await ctx.SaveChangesAsync();
    }

    private async Task<CategoriaDocumentoView?> ObterPorIdAsync(Guid id)
    {
        await using UniPlusDbContext leituraCtx = _fixture.CreateDbContext(userId: null);

        return await new CategoriaDocumentoReader(leituraCtx).ObterPorIdAsync(id);
    }

    private async Task<string[]> CodigosVivosComPrefixoAsync(string prefixo)
    {
        await using UniPlusDbContext leituraCtx = _fixture.CreateDbContext(userId: null);
        IReadOnlyList<CategoriaDocumentoView> todas = await new CategoriaDocumentoReader(leituraCtx).ListarVivosAsync();

        return [.. todas
            .Select(v => v.Codigo)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];
    }

    private static string CodigoUnico() => $"CAT_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
