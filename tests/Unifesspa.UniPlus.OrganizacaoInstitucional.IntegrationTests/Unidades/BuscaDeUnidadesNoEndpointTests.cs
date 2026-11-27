namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP da busca textual de <c>GET /api/organizacao/unidades?q=</c>: texto em branco ou só de caractere
/// nulo não é busca, o texto longo demais é recusado com 422, os links de navegação ecoam o <c>q</c> sem os espaços das pontas e
/// o cursor não carrega o filtro.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class BuscaDeUnidadesNoEndpointTests
{
    private const string CodigoDaRecusa = "uniplus.consulta.busca_muito_longa";

    private readonly OrganizacaoEndpointFixture _fixture;

    public BuscaDeUnidadesNoEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Texto só de espaços devolve os mesmos itens e as mesmas páginas que a listagem sem q")]
    public async Task SoEspacos_DevolveAsMesmasPaginasQueSemQ()
    {
        await SemearAsync(5);
        using HttpClient client = _fixture.Factory.CreateClient();

        IReadOnlyList<PaginaDeUnidades> semQ = await PaginaDeUnidades.PercorrerAsync(client, "limit=2");
        IReadOnlyList<PaginaDeUnidades> soEspacos = await PaginaDeUnidades.PercorrerAsync(client, "q=%20%20%20&limit=2");

        semQ.Should().HaveCountGreaterThan(2);
        IdsPorPagina(soEspacos).Should().Equal(IdsPorPagina(semQ));
    }

    [Fact(DisplayName = "Seguir o next de uma busca só de espaços devolve a página seguinte da listagem")]
    public async Task SoEspacos_SeguirONext_DevolveAPaginaSeguinte()
    {
        await SemearAsync(5);
        using HttpClient client = _fixture.Factory.CreateClient();
        IReadOnlyList<PaginaDeUnidades> semQ = await PaginaDeUnidades.PercorrerAsync(client, "limit=2");

        PaginaDeUnidades primeira = await PaginaDeUnidades.ConsultarAsync(client, "q=%20%20%20&limit=2");
        PaginaDeUnidades segunda = await PaginaDeUnidades.LerAsync(client, primeira.Next!);

        segunda.Status.Should().Be(HttpStatusCode.OK);
        segunda.Ids.Should().Equal(semQ[1].Ids);
    }

    [Fact(DisplayName = "Os links self e next ecoam o q sem os espaços das pontas")]
    public async Task Links_EcoamOQSemOsEspacosDasPontas()
    {
        string token = NovoToken();
        await SemearAsync(2, token);
        string q = $"  {token}  ";
        using HttpClient client = _fixture.Factory.CreateClient();

        PaginaDeUnidades pagina = await PaginaDeUnidades.ConsultarAsync(client, $"q={Uri.EscapeDataString(q)}&limit=1");

        PaginaDeUnidades.ParametroDe(pagina.Self!, "q").Should().Be(token);
        PaginaDeUnidades.ParametroDe(pagina.Next!, "q").Should().Be(token);
        (await PaginaDeUnidades.LerAsync(client, pagina.Next!)).Status.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "Texto só de caractere nulo, com ou sem espaços, não é busca e não dá 500")]
    [InlineData("%00")]
    [InlineData("%00%00")]
    [InlineData("%20%00%20")]
    public async Task SoCaractereNulo_DevolveAListagemSemFiltro(string q)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        PaginaDeUnidades semQ = await PaginaDeUnidades.ConsultarAsync(client, "limit=2");

        PaginaDeUnidades comNulo = await PaginaDeUnidades.ConsultarAsync(client, $"q={q}&limit=2");

        comNulo.Status.Should().Be(HttpStatusCode.OK);
        comNulo.Ids.Should().Equal(semQ.Ids);
    }

    [Fact(DisplayName = "Caractere nulo misturado ao texto é descartado e a busca segue pelo resto")]
    public async Task NuloMisturadoAoTexto_BuscaPeloResto()
    {
        string token = NovoToken();
        await SemearAsync(1, token);
        using HttpClient client = _fixture.Factory.CreateClient();

        PaginaDeUnidades pagina = await PaginaDeUnidades.ConsultarAsync(client, $"q={token[..5]}%00{token[5..]}");

        pagina.Status.Should().Be(HttpStatusCode.OK);
        pagina.Ids.Should().ContainSingle();
    }

    [Theory(DisplayName = "Os curingas % e _ e a barra invertida digitados são texto")]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task Curingas_SaoTexto(string curinga)
    {
        string token = NovoToken();
        Guid comCuringa = await _fixture.SemearAsync(CadastroDeUnidadeDeTeste.ComNome($"Setor {token}{curinga}fim"));
        Guid comOutraLetra = await _fixture.SemearAsync(CadastroDeUnidadeDeTeste.ComNome($"Setor {token}Zfim"));
        using HttpClient client = _fixture.Factory.CreateClient();

        PaginaDeUnidades pagina = await PaginaDeUnidades.ConsultarAsync(
            client, $"q={Uri.EscapeDataString($"{token}{curinga}fim")}");

        pagina.Ids.Should().Contain(comCuringa);
        pagina.Ids.Should().NotContain(comOutraLetra);
    }

    [Theory(DisplayName = "Texto de 201 caracteres ou mais é recusado com 422 de busca muito longa")]
    [InlineData(201)]
    [InlineData(5_000)]
    public async Task TextoLongoDemais_Retorna422(int tamanho)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        PaginaDeUnidades pagina = await PaginaDeUnidades.ConsultarAsync(client, $"q={new string('a', tamanho)}");

        pagina.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        pagina.Corpo.Should().Contain(CodigoDaRecusa);
    }

    [Theory(DisplayName = "O cursor da próxima página só carrega o ponto de continuação e o resumo da consulta, qualquer que seja o q")]
    [InlineData("q=&")]
    [InlineData("")]
    [InlineData("q=a%25b&")]
    public async Task Cursor_NaoCarregaOFiltro(string consulta)
    {
        await SemearAsync(2, token: null, nome: "Núcleo a%b");
        using HttpClient client = _fixture.Factory.CreateClient();

        PaginaDeUnidades pagina = await PaginaDeUnidades.ConsultarAsync(client, $"{consulta}limit=1");
        CursorPayload payload = await DecodificarAsync(PaginaDeUnidades.ParametroDe(pagina.Next!, "cursor")!);

        payload.After.Should().Be(pagina.Ids.Single().ToString());
        payload.Should().BeEquivalentTo(
            new { Limit = 1, ResourceTag = "unidades", Direction = PaginationDirection.Next, ResumoDaConta = (string?)null });
        payload.SortKey.Should().NotBeNullOrWhiteSpace().And.NotContain("a%b").And.NotContain("Núcleo");
    }

    private static string NovoToken() => Guid.NewGuid().ToString("N")[..10];

    private static IEnumerable<string> IdsPorPagina(IEnumerable<PaginaDeUnidades> paginas) =>
        paginas.Select(pagina => string.Join(',', pagina.Ids));

    private async Task SemearAsync(int quantidade, string? token = null, string nome = "Setor")
    {
        for (int i = 0; i < quantidade; i++)
        {
            await _fixture.SemearAsync(CadastroDeUnidadeDeTeste.ComNome($"{nome} {token} {i}"));
        }
    }

    private async Task<CursorPayload> DecodificarAsync(string cursor)
    {
        CursorEncoder encoder = _fixture.Factory.Services.GetRequiredService<CursorEncoder>();
        CursorDecodeResult resultado = await encoder.TryDecodeAsync(cursor);
        return resultado.Payload!;
    }
}
