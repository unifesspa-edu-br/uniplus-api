namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura em lote do catálogo contra Postgres real: mesma vigência e mesma projeção da
/// leitura individual, numa consulta só.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoAtoPublicadoReaderTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly Meio = new(2026, 6, 1);

    private readonly PublicacoesDbFixture _fixture;

    public TipoAtoPublicadoReaderTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ListarVigentes em fevereiro devolve só os códigos pedidos, na primeira versão")]
    public async Task ListarVigentes_EmFevereiro_DevolveAPrimeiraVersaoDosCodigosPedidos()
    {
        CatalogoGravado catalogo = await GravarCatalogoAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoAtoPublicadoReader(ctx);

        IReadOnlyList<TipoAtoPublicadoView> emFevereiro = await reader
            .ListarVigentesAsync([catalogo.Unico, catalogo.Comum, catalogo.Expirado], new DateOnly(2026, 2, 1), CancellationToken.None);

        emFevereiro.Select(t => t.Codigo).Should().BeEquivalentTo([catalogo.Unico, catalogo.Comum, catalogo.Expirado]);
        emFevereiro.Single(t => t.Codigo == catalogo.Comum).UnicoPorObjeto.Should().BeFalse("em fevereiro vigora a primeira versão");
    }

    [Fact(DisplayName = "ListarVigentes em julho devolve a segunda versão, ignora o código sem versão vigente e projeta como a leitura individual")]
    public async Task ListarVigentes_EmJulho_DevolveASegundaVersaoEIgnoraOExpirado()
    {
        CatalogoGravado catalogo = await GravarCatalogoAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoAtoPublicadoReader(ctx);

        IReadOnlyList<TipoAtoPublicadoView> emJulho = await reader
            .ListarVigentesAsync([catalogo.Unico, catalogo.Comum, catalogo.Expirado], new DateOnly(2026, 7, 1), CancellationToken.None);

        emJulho.Select(t => t.Codigo).Should().BeEquivalentTo([catalogo.Unico, catalogo.Comum], "a versão de 'expirado' terminou antes");
        emJulho.Single(t => t.Codigo == catalogo.Comum).UnicoPorObjeto.Should().BeTrue("em julho vigora a segunda versão");

        TipoAtoPublicadoView? individual = await reader.ObterVigenteAsync(catalogo.Unico, new DateOnly(2026, 7, 1), CancellationToken.None);
        emJulho.Single(t => t.Codigo == catalogo.Unico).Should().Be(individual, "o lote e a leitura individual projetam o mesmo");
    }

    private async Task<CatalogoGravado> GravarCatalogoAsync()
    {
        CatalogoGravado catalogo = new(CodigoUnico(), CodigoUnico(), CodigoUnico());
        await Gravar(
            Novo(catalogo.Unico, Inicio, null, unicoPorObjeto: true),
            Novo(catalogo.Comum, Inicio, Meio, unicoPorObjeto: false),
            Novo(catalogo.Comum, Meio, null, unicoPorObjeto: true),
            Novo(catalogo.Expirado, Inicio, Meio, unicoPorObjeto: false),
            Novo(CodigoUnico(), Inicio, null, unicoPorObjeto: false));

        return catalogo;
    }

    private async Task Gravar(params TipoAtoPublicado[] tipos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext("admin-publicacoes");
        ctx.TiposAtoPublicado.AddRange(tipos);
        await ctx.SaveChangesAsync();
    }

    private static TipoAtoPublicado Novo(string codigo, DateOnly inicio, DateOnly? fim, bool unicoPorObjeto) =>
        new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = "Tipo de ato de teste",
            CongelaConfiguracao = true,
            UnicoPorObjeto = unicoPorObjeto,
            VigenciaInicio = inicio,
            VigenciaFim = fim,
        }.Criar().Value!;

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "TESTE_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }

    private sealed record CatalogoGravado(string Unico, string Comum, string Expirado);
}
