namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de tipos de deficiência por código (ADR-0056), contra Postgres real. É ela que diz ao documento da
/// configuração quais cadastros citados existem: um cadastro removido que continuasse respondendo faria o documento
/// citar algo que saiu do catálogo. Cada teste cria os próprios cadastros, com código único.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoDeficienciaReaderPorCodigosTests
{
    private const string Admin = "admin-a";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly string _prefixo = $"DEF_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    public TipoDeficienciaReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync devolve só os cadastros vivos pedidos, ordenados por código")]
    public async Task ListarVivosPorCodigosAsync_DevolveSoOsPedidosOrdenados()
    {
        (string a, string b, string naoPedido) = (Codigo("A"), Codigo("B"), Codigo("C"));
        await CadastrarAsync(vivos: [b, a, naoPedido], removidos: []);

        IReadOnlyList<TipoDeficienciaView> cadastros = await ListarAsync(b, a);

        cadastros.Select(c => c.Codigo).Should().Equal(a, b);
        cadastros.Should().OnlyContain(c => c.Id != Guid.Empty);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync não devolve código inexistente, escrito de outra forma, vazio nem com caractere nulo")]
    public async Task ListarVivosPorCodigosAsync_CodigoSemCadastro_NaoVolta()
    {
        string vivo = Codigo("A");
        await CadastrarAsync(vivos: [vivo], removidos: []);

        IReadOnlyList<TipoDeficienciaView> cadastros =
            await ListarAsync(vivo, Codigo("INEXISTENTE"), $" {vivo} ", string.Empty, $"{vivo}\0");

        cadastros.Select(c => c.Codigo).Should().Equal(vivo);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync não devolve cadastro removido")]
    public async Task ListarVivosPorCodigosAsync_CadastroRemovido_NaoVolta()
    {
        (string vivo, string removido) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivos: [vivo], removidos: [removido]);

        IReadOnlyList<TipoDeficienciaView> cadastros = await ListarAsync(vivo, removido);

        cadastros.Select(c => c.Codigo).Should().Equal(vivo);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync com códigos repetidos devolve cada cadastro uma só vez")]
    public async Task ListarVivosPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaCadastro()
    {
        (string a, string b) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivos: [a, b], removidos: []);

        IReadOnlyList<TipoDeficienciaView> cadastros = await ListarAsync(a, b, a);

        cadastros.Select(c => c.Codigo).Should().Equal(a, b);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync com lote vazio não devolve cadastro")]
    public async Task ListarVivosPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivos: [Codigo("A")], removidos: []);

        IReadOnlyList<TipoDeficienciaView> cadastros = await ListarAsync();

        cadastros.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<TipoDeficienciaView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new TipoDeficienciaReader(leitura).ListarVivosPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivos, string[] removidos)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(Admin);
        List<TipoDeficiencia> cadastros = [.. vivos.Concat(removidos).Select(Novo)];
        escrita.TiposDeficiencia.AddRange(cadastros);
        await escrita.SaveChangesAsync();

        escrita.TiposDeficiencia.RemoveRange(cadastros.Where(c => removidos.Contains(c.Codigo.Valor)));
        await escrita.SaveChangesAsync();
    }

    private static TipoDeficiencia Novo(string codigo) =>
        TipoDeficiencia.Criar(codigo, $"Tipo {codigo}", "Descrição de teste").Value!;

    private string Codigo(string sufixo) => $"{_prefixo}_{sufixo}";
}
