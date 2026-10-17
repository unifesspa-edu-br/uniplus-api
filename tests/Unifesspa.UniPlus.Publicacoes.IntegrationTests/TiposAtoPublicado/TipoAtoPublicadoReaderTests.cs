namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

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

    [Fact(DisplayName = "ListarVigentes devolve só os códigos pedidos, na versão vigente da data, e ignora o código sem versão vigente")]
    public async Task ListarVigentes_DevolveAVersaoVigenteDosCodigosPedidos()
    {
        string unico = CodigoUnico();
        string comum = CodigoUnico();
        string expirado = CodigoUnico();
        string foraDoPedido = CodigoUnico();
        await Gravar(
            Novo(unico, Inicio, null, unicoPorObjeto: true),
            Novo(comum, Inicio, Meio, unicoPorObjeto: false),
            Novo(comum, Meio, null, unicoPorObjeto: true),
            Novo(expirado, Inicio, Meio, unicoPorObjeto: false),
            Novo(foraDoPedido, Inicio, null, unicoPorObjeto: false));

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoAtoPublicadoReader(ctx);

        IReadOnlyList<TipoAtoPublicadoView> emJaneiro = await reader
            .ListarVigentesAsync([unico, comum, expirado], new DateOnly(2026, 2, 1), CancellationToken.None);
        IReadOnlyList<TipoAtoPublicadoView> emJulho = await reader
            .ListarVigentesAsync([unico, comum, expirado], new DateOnly(2026, 7, 1), CancellationToken.None);

        emJaneiro.Select(t => t.Codigo).Should().BeEquivalentTo([unico, comum, expirado]);
        emJaneiro.Single(t => t.Codigo == comum).UnicoPorObjeto.Should().BeFalse("em fevereiro vigora a primeira versão");
        emJulho.Select(t => t.Codigo).Should().BeEquivalentTo([unico, comum], "a versão de 'expirado' terminou antes");
        emJulho.Single(t => t.Codigo == comum).UnicoPorObjeto.Should().BeTrue("em julho vigora a segunda versão");

        TipoAtoPublicadoView? individual = await reader.ObterVigenteAsync(unico, new DateOnly(2026, 7, 1), CancellationToken.None);
        emJulho.Single(t => t.Codigo == unico).Should().Be(individual, "o lote e a leitura individual projetam o mesmo");
    }

    [Fact(DisplayName = "ListarTiposDosAtos devolve o código de cada ato pedido uma vez só, sem filtrar pela unicidade gravada nem depender do nome")]
    public async Task ListarTiposDosAtos_DevolveOsCodigosDistintosDosAtosPedidos()
    {
        string tipoA = CodigoUnico();
        string tipoB = CodigoUnico();
        string tipoForaDoPedido = CodigoUnico();
        AtoNormativo aComNome = Ato(tipoA, tipoNome: "Nome de A", unicoPorObjeto: true);
        AtoNormativo aComOutroNome = Ato(tipoA, tipoNome: "Outro nome de A", unicoPorObjeto: false);
        AtoNormativo bSemNome = Ato(tipoB, tipoNome: null, unicoPorObjeto: false);
        AtoNormativo foraDoPedido = Ato(tipoForaDoPedido, tipoNome: "Fora", unicoPorObjeto: true);
        await using (PublicacoesDbContext gravacao = _fixture.CreateDbContext(userId: null))
        {
            gravacao.AtosNormativos.AddRange(aComNome, aComOutroNome, bSemNome, foraDoPedido);
            await gravacao.SaveChangesAsync();
        }

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new VagaDeLinhagemReader(ctx);

        IReadOnlyList<string> codigos = await reader.ListarTiposDosAtosAsync(
            [aComNome.Id, aComOutroNome.Id, bSemNome.Id], CancellationToken.None);

        codigos.Should().BeEquivalentTo(
            [tipoA, tipoB],
            "dois atos do mesmo tipo com nomes diferentes dão um código só, o tipo de unicidade gravada falsa também entra e o ato fora do pedido não");
    }

    private static AtoNormativo Ato(string tipoCodigo, string? tipoNome, bool unicoPorObjeto) =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            orgao: "CEPS",
            serie: "EDITAL",
            ano: 2026,
            numero: Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8],
            tipoCodigo: tipoCodigo,
            congelaConfiguracao: false,
            efeitoIrreversivel: false,
            unicoPorObjeto: unicoPorObjeto,
            dataPublicacao: new DateOnly(2026, 3, 13),
            documentoHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            assinante: "Jairo Belchior",
            registradoEm: new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero),
            versaoInvocada: null,
            tipoNome: tipoNome);

    private async Task Gravar(params TipoAtoPublicado[] tipos)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext("admin-publicacoes");
        ctx.TiposAtoPublicado.AddRange(tipos);
        await ctx.SaveChangesAsync();
    }

    private static TipoAtoPublicado Novo(string codigo, DateOnly inicio, DateOnly? fim, bool unicoPorObjeto) =>
        TipoAtoPublicado.Criar(
            codigo,
            nome: "Tipo de ato de teste",
            congelaConfiguracao: true,
            unicoPorObjeto: unicoPorObjeto,
            efeitoIrreversivel: false,
            ehResultado: false,
            vigenciaInicio: inicio,
            vigenciaFim: fim,
            baseLegal: null).Value!;

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "TESTE_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
