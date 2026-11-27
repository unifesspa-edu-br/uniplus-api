namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ReferenciasReservaDemografica;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de referências de reserva demográfica por código (ADR-0056), contra Postgres real. É ela que diz ao documento da
/// configuração quais cadastros citados existem: um cadastro removido que continuasse respondendo faria o documento
/// citar algo que saiu do catálogo. Cada teste cria os próprios cadastros, com código único.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ReferenciaReservaDemograficaReaderPorCodigosTests
{
    private const string Admin = "admin-a";

    private readonly ConfiguracaoDbFixture _fixture;
    private readonly string _prefixo = $"CENSO{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    public ReferenciaReservaDemograficaReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync devolve só os cadastros vivos pedidos, ordenados por censo de referência")]
    public async Task ListarVivasPorCodigosAsync_DevolveSoOsPedidosOrdenados()
    {
        (string a, string b, string naoPedido) = (Codigo("A"), Codigo("B"), Codigo("C"));
        await CadastrarAsync(vivos: [b, a, naoPedido], removidos: []);

        IReadOnlyList<ReferenciaReservaDemograficaView> cadastros = await ListarAsync(b, a);

        cadastros.Select(c => c.CensoReferencia).Should().Equal(a, b);
        cadastros.Should().OnlyContain(c => c.Id != Guid.Empty);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve código inexistente, escrito de outra forma, vazio nem com caractere nulo")]
    public async Task ListarVivasPorCodigosAsync_CodigoSemCadastro_NaoVolta()
    {
        string vivo = Codigo("A");
        await CadastrarAsync(vivos: [vivo], removidos: []);

        IReadOnlyList<ReferenciaReservaDemograficaView> cadastros =
            await ListarAsync(vivo, Codigo("INEXISTENTE"), $" {vivo} ", string.Empty, $"{vivo}\0");

        cadastros.Select(c => c.CensoReferencia).Should().Equal(vivo);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync não devolve cadastro removido")]
    public async Task ListarVivasPorCodigosAsync_CadastroRemovido_NaoVolta()
    {
        (string vivo, string removido) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivos: [vivo], removidos: [removido]);

        IReadOnlyList<ReferenciaReservaDemograficaView> cadastros = await ListarAsync(vivo, removido);

        cadastros.Select(c => c.CensoReferencia).Should().Equal(vivo);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com códigos repetidos devolve cada cadastro uma só vez")]
    public async Task ListarVivasPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaCadastro()
    {
        (string a, string b) = (Codigo("A"), Codigo("B"));
        await CadastrarAsync(vivos: [a, b], removidos: []);

        IReadOnlyList<ReferenciaReservaDemograficaView> cadastros = await ListarAsync(a, b, a);

        cadastros.Select(c => c.CensoReferencia).Should().Equal(a, b);
    }

    [Fact(DisplayName = "ListarVivasPorCodigosAsync com lote vazio não devolve cadastro")]
    public async Task ListarVivasPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivos: [Codigo("A")], removidos: []);

        IReadOnlyList<ReferenciaReservaDemograficaView> cadastros = await ListarAsync();

        cadastros.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<ReferenciaReservaDemograficaView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new ReferenciaReservaDemograficaReader(leitura).ListarVivasPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivos, string[] removidos)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(Admin);
        List<ReferenciaReservaDemografica> cadastros = [.. vivos.Concat(removidos).Select(Novo)];
        escrita.ReferenciasReservaDemografica.AddRange(cadastros);
        await escrita.SaveChangesAsync();

        escrita.ReferenciasReservaDemografica.RemoveRange(cadastros.Where(c => removidos.Contains(c.CensoReferencia)));
        await escrita.SaveChangesAsync();
    }

    private static ReferenciaReservaDemografica Novo(string censo) =>
        ReferenciaReservaDemografica.Criar(censo, 10m, 2m, 5m, "Lei 12.711/2012, art. 10, III").Value!;

    private string Codigo(string sufixo) => $"{_prefixo}_{sufixo}";
}
