namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de categorias de documento por código (ADR-0056), contra Postgres real. É ela
/// que diz ao documento da configuração quais categorias citadas existem: uma categoria removida
/// que continuasse respondendo faria a banca ser competente por um rótulo que saiu do catálogo.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class CategoriaDocumentoReaderPorCodigosTests
{
    private const string Admin = "admin-a";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly string _prefixo = $"CAT_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    public CategoriaDocumentoReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync devolve só as categorias vivas pedidas, ordenadas por código")]
    public async Task ListarVivasPorCodigosAsync_DevolveSoAsPedidasOrdenadas()
    {
        (string a, string b, string naoPedida) = (Codigo("A"), Codigo("B"), Codigo("C"));
        await CadastrarAsync(vivas: [b, a, naoPedida], removidas: []);

        IReadOnlyList<CategoriaDocumentoView> categorias = await ListarAsync(b, a);

        categorias.Select(c => c.Codigo).Should().Equal(a, b);
        categorias.Should().OnlyContain(c => c.Id != Guid.Empty && c.Nome.Length > 0);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve código inexistente nem fora do formato")]
    public async Task ListarVivasPorCodigosAsync_CodigoInexistenteOuMalFormado_NaoVolta()
    {
        string viva = Codigo("A");
        await CadastrarAsync(vivas: [viva], removidas: []);

        IReadOnlyList<CategoriaDocumentoView> categorias =
            await ListarAsync(viva, Codigo("INEXISTENTE"), "categoria-minuscula", string.Empty);

        categorias.Select(c => c.Codigo).Should().Equal(viva);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve categoria removida")]
    public async Task ListarVivasPorCodigosAsync_CategoriaRemovida_NaoVolta()
    {
        (string viva, string removida) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivas: [viva], removidas: [removida]);

        IReadOnlyList<CategoriaDocumentoView> categorias = await ListarAsync(viva, removida);

        categorias.Select(c => c.Codigo).Should().Equal(viva);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com códigos repetidos devolve cada categoria uma só vez")]
    public async Task ListarVivasPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaCategoria()
    {
        (string a, string b) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivas: [a, b], removidas: []);

        IReadOnlyList<CategoriaDocumentoView> categorias = await ListarAsync(a, b, a, $" {a} ");

        categorias.Select(c => c.Codigo).Should().Equal(a, b);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com lote vazio não devolve categoria")]
    public async Task ListarVivasPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivas: [Codigo("A")], removidas: []);

        IReadOnlyList<CategoriaDocumentoView> categorias = await ListarAsync();

        categorias.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<CategoriaDocumentoView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new CategoriaDocumentoReader(leitura).ListarVivasPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivas, string[] removidas)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(Admin);
        List<CategoriaDocumento> categorias = [.. vivas.Concat(removidas).Select(Categoria)];
        escrita.CategoriasDocumento.AddRange(categorias);
        await escrita.SaveChangesAsync();

        escrita.CategoriasDocumento.RemoveRange(categorias.Where(c => removidas.Contains(c.Codigo.Valor)));
        await escrita.SaveChangesAsync();
    }

    private static CategoriaDocumento Categoria(string codigo) =>
        CategoriaDocumento.Criar(codigo, $"Categoria {codigo}", null, 0).Value!;

    private string Codigo(string sufixo) => $"{_prefixo}_{sufixo}";
}
