namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// O que a busca textual de <c>GET /api/organizacao/unidades?q=</c> já garantia antes de passar a usar a
/// normalização da plataforma e que continua valendo: curinga digitado é texto, as pontas são aparadas, as letras
/// acentuadas do português casam sem acento e sem caixa, a ausência de alias não quebra a consulta e o limite do
/// texto pesquisado é de 200 caracteres.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class BuscaDeUnidadesCaracterizacaoTests
{
    private const string Acentuadas = "ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý";
    private const string SemAcento = "AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny";

    private readonly OrganizacaoEndpointFixture _fixture;

    public BuscaDeUnidadesCaracterizacaoTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    public static IEnumerable<object[]> LetrasDaTabela => Acentuadas.Select(letra => new object[] { letra });

    [Fact(DisplayName = "A barra invertida digitada é texto: só acha a unidade que a contém")]
    public async Task BarraInvertida_EhTexto()
    {
        string token = NovoToken();
        Guid comBarra = await SemearAsync($"Divisão {token}\\Beta");
        Guid semBarra = await SemearAsync($"Divisão {token}Beta");
        Guid comOutraLetra = await SemearAsync($"Divisão {token}ZBeta");

        PaginaDeUnidades pagina = await ConsultarAsync($"{token}\\Beta");

        pagina.Ids.Should().Contain(comBarra);
        pagina.Ids.Should().NotContain([semBarra, comOutraLetra]);
    }

    [Fact(DisplayName = "Os espaços nas pontas do texto pesquisado são aparados")]
    public async Task EspacosNasPontas_SaoAparados()
    {
        string token = NovoToken();
        Guid id = await SemearAsync($"Faculdade {token}");

        PaginaDeUnidades pagina = await ConsultarAsync($"  {token}  ");

        pagina.Ids.Should().Contain(id);
    }

    [Theory(DisplayName = "A letra acentuada do português casa sem acento e sem caixa, nas duas pontas")]
    [MemberData(nameof(LetrasDaTabela))]
    public async Task LetraDaTabela_CasaSemAcentoESemCaixa(char letra)
    {
        string token = NovoToken();
        Guid id = await SemearAsync($"Núcleo {token}{letra}fim");
        char semAcento = SemAcento[Acentuadas.IndexOf(letra, StringComparison.Ordinal)];

        foreach (char variante in new[] { letra, semAcento, char.ToUpperInvariant(semAcento), char.ToLowerInvariant(letra) })
        {
            PaginaDeUnidades pagina = await ConsultarAsync($"{token}{variante}fim");
            pagina.Ids.Should().Contain(id, $"o termo com '{variante}' deve achar o nome com '{letra}'");
        }
    }

    [Fact(DisplayName = "A unidade sem alias aparece pela busca em outro campo e não quebra a consulta")]
    public async Task AliasNulo_NaoQuebraABuscaEmOutroCampo()
    {
        string token = NovoToken();
        Guid semAlias = await SemearAsync($"Instituto {token}");
        Guid comAlias = await SemearAsync($"Coordenação {token}", alias: $"Apelido {token}");

        PaginaDeUnidades pagina = await ConsultarAsync(token);

        pagina.Status.Should().Be(HttpStatusCode.OK);
        pagina.Ids.Should().Contain([semAlias, comAlias]);
    }

    [Fact(DisplayName = "Texto pesquisado de 200 caracteres é aceito")]
    public async Task TextoDe200Caracteres_EhAceito()
    {
        PaginaDeUnidades pagina = await ConsultarAsync(new string('a', 200));

        pagina.Status.Should().Be(HttpStatusCode.OK);
    }

    private static string NovoToken() => Guid.NewGuid().ToString("N")[..10];

    private Task<Guid> SemearAsync(string nome, string? alias = null) =>
        _fixture.SemearAsync(CadastroDeUnidadeDeTeste.ComNome(nome, alias));

    private async Task<PaginaDeUnidades> ConsultarAsync(string texto)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        return await PaginaDeUnidades.ConsultarAsync(client, $"q={Uri.EscapeDataString(texto)}&limit=100");
    }
}
