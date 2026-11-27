namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de recursos de acessibilidade por código (ADR-0056), contra Postgres real. É ela que diz ao documento da
/// configuração quais cadastros citados existem: um cadastro removido que continuasse respondendo faria o documento
/// citar algo que saiu do catálogo. Cada teste cria os próprios cadastros, com código único.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class RecursoAcessibilidadeReaderPorCodigosTests
{
    private const string Admin = "admin-a";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly string _prefixo = $"REC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    public RecursoAcessibilidadeReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync devolve só os cadastros vivos pedidos, ordenados por código")]
    public async Task ListarVivosPorCodigosAsync_DevolveSoOsPedidosOrdenados()
    {
        (string a, string b, string naoPedido) = (Codigo("A"), Codigo("B"), Codigo("C"));
        await CadastrarAsync(vivos: [b, a, naoPedido], removidos: []);

        IReadOnlyList<RecursoAcessibilidadeView> cadastros = await ListarAsync(b, a);

        cadastros.Select(c => c.Codigo).Should().Equal(a, b);
        cadastros.Should().OnlyContain(c => c.Id != Guid.Empty);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync não devolve código inexistente, escrito de outra forma, vazio nem com caractere nulo")]
    public async Task ListarVivosPorCodigosAsync_CodigoSemCadastro_NaoVolta()
    {
        string vivo = Codigo("A");
        await CadastrarAsync(vivos: [vivo], removidos: []);

        IReadOnlyList<RecursoAcessibilidadeView> cadastros =
            await ListarAsync(vivo, Codigo("INEXISTENTE"), $" {vivo} ", string.Empty, $"{vivo}\0");

        cadastros.Select(c => c.Codigo).Should().Equal(vivo);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync não devolve cadastro removido")]
    public async Task ListarVivosPorCodigosAsync_CadastroRemovido_NaoVolta()
    {
        (string vivo, string removido) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivos: [vivo], removidos: [removido]);

        IReadOnlyList<RecursoAcessibilidadeView> cadastros = await ListarAsync(vivo, removido);

        cadastros.Select(c => c.Codigo).Should().Equal(vivo);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync com códigos repetidos devolve cada cadastro uma só vez")]
    public async Task ListarVivosPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaCadastro()
    {
        (string a, string b) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivos: [a, b], removidos: []);

        IReadOnlyList<RecursoAcessibilidadeView> cadastros = await ListarAsync(a, b, a);

        cadastros.Select(c => c.Codigo).Should().Equal(a, b);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync com lote vazio não devolve cadastro")]
    public async Task ListarVivosPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivos: [Codigo("A")], removidos: []);

        IReadOnlyList<RecursoAcessibilidadeView> cadastros = await ListarAsync();

        cadastros.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<RecursoAcessibilidadeView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new RecursoAcessibilidadeReader(leitura).ListarVivosPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivos, string[] removidos)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(Admin);
        List<RecursoAcessibilidade> cadastros = [.. vivos.Concat(removidos).Select(Novo)];
        escrita.RecursosAcessibilidade.AddRange(cadastros);
        await escrita.SaveChangesAsync();

        escrita.RecursosAcessibilidade.RemoveRange(cadastros.Where(c => removidos.Contains(c.Codigo.Valor)));
        await escrita.SaveChangesAsync();
    }

    private static RecursoAcessibilidade Novo(string codigo) =>
        RecursoAcessibilidade.Criar(codigo, $"Recurso {codigo}", "Recurso de acessibilidade").Value!;

    private string Codigo(string sufixo) => $"{_prefixo}_{sufixo}";
}
