namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposBanca;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A leitura em lote de tipos de banca por código (ADR-0056), contra Postgres real. É ela que
/// diz ao documento da configuração quais tipos de banca citados existem: um tipo removido que
/// continuasse respondendo faria a fase exigir uma banca que saiu do cadastro.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoBancaReaderPorCodigosTests : IAsyncLifetime
{
    private const string Admin = "admin-a";
    private const string Biopsicossocial = "BANCA_BIOPSICOSSOCIAL";
    private const string Heteroidentificacao = "BANCA_HETEROIDENTIFICACAO";
    private const string CorrecaoDeRedacoes = "BANCA_CORRECAO_REDACOES";

    private static readonly string[] CodigosUsados = [Biopsicossocial, Heteroidentificacao, CorrecaoDeRedacoes];

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoBancaReaderPorCodigosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => ApagarTiposUsadosAsync();

    public Task DisposeAsync() => ApagarTiposUsadosAsync();

    [Fact(DisplayName = "ListarVivosPorCodigosAsync devolve só os tipos vivos pedidos, ordenados por código")]
    public async Task ListarVivosPorCodigosAsync_DevolveSoOsPedidosOrdenados()
    {
        await CadastrarAsync(vivos: [Biopsicossocial, Heteroidentificacao, CorrecaoDeRedacoes], removidos: []);

        IReadOnlyList<TipoBancaView> tipos = await ListarAsync(Heteroidentificacao, Biopsicossocial);

        tipos.Select(t => t.Codigo).Should().Equal(Biopsicossocial, Heteroidentificacao);
        tipos.Should().OnlyContain(t => t.Id != Guid.Empty && t.Nome.Length > 0);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync não devolve código inexistente nem fora do formato")]
    public async Task ListarVivosPorCodigosAsync_CodigoInexistenteOuMalFormado_NaoVolta()
    {
        await CadastrarAsync(vivos: [Biopsicossocial], removidos: []);

        IReadOnlyList<TipoBancaView> tipos =
            await ListarAsync(Biopsicossocial, "BANCA_QUE_NAO_EXISTE", "banca-minuscula", string.Empty);

        tipos.Select(t => t.Codigo).Should().Equal(Biopsicossocial);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync não devolve tipo de banca removido")]
    public async Task ListarVivosPorCodigosAsync_TipoRemovido_NaoVolta()
    {
        await CadastrarAsync(vivos: [Biopsicossocial], removidos: [Heteroidentificacao]);

        IReadOnlyList<TipoBancaView> tipos = await ListarAsync(Biopsicossocial, Heteroidentificacao);

        tipos.Select(t => t.Codigo).Should().Equal(Biopsicossocial);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync com códigos repetidos devolve cada tipo uma só vez")]
    public async Task ListarVivosPorCodigosAsync_LoteComRepetidos_DevolveUmaVezCadaTipo()
    {
        await CadastrarAsync(vivos: [Biopsicossocial, Heteroidentificacao], removidos: []);

        IReadOnlyList<TipoBancaView> tipos =
            await ListarAsync(Biopsicossocial, Heteroidentificacao, Biopsicossocial, $" {Biopsicossocial} ");

        tipos.Select(t => t.Codigo).Should().Equal(Biopsicossocial, Heteroidentificacao);
    }

    [Fact(DisplayName = "ListarVivosPorCodigosAsync com lote vazio não devolve tipo")]
    public async Task ListarVivosPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivos: [Biopsicossocial], removidos: []);

        IReadOnlyList<TipoBancaView> tipos = await ListarAsync();

        tipos.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<TipoBancaView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await new TipoBancaReader(leitura).ListarVivosPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivos, string[] removidos)
    {
        await using UniPlusDbContext escrita = _fixture.CreateDbContext(Admin);
        List<TipoBanca> tipos = [.. vivos.Concat(removidos).Select(Tipo)];
        escrita.TiposBanca.AddRange(tipos);
        await escrita.SaveChangesAsync();

        escrita.TiposBanca.RemoveRange(tipos.Where(t => removidos.Contains(t.Codigo.Valor)));
        await escrita.SaveChangesAsync();
    }

    private async Task ApagarTiposUsadosAsync()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM configuracao.tipos_de_banca WHERE codigo = ANY(@p0)",
            new NpgsqlParameter("p0", CodigosUsados));
    }

    private static TipoBanca Tipo(string codigo) =>
        TipoBanca.Criar(codigo, $"Banca {codigo}", null, null).Value!;
}
