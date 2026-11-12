namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O registro das decisões da proteção do catálogo pelo host real (ADR-0153): o Representante do controlador consulta as
/// decisões com filtros, da mais recente para a mais antiga, em páginas.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class DecisoesDaProtecaoEndpointTests : HistoricoDaProtecaoTestBase
{
    public DecisoesDaProtecaoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "As decisões saem das mais recentes para as mais antigas, e o filtro de ato e o de cadastro restringem o recorte")]
    public async Task Decisoes_OrdemEFiltrosDeAtoECadastro()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        (await NomesAsync(client, Betina, $"busca={marca}")).Should().Equal(Nomes(marca, "Laudo médico complementar", "Renda familiar bruta", "Certidão de nascimento"));
        (await NomesAsync(client, Betina, $"busca={marca}&ato=REPROVACAO")).Should().Equal(Nomes(marca, "Renda familiar bruta"));
        (await NomesAsync(client, Betina, $"busca={marca}&ato=REPROVACAO&ato=APROVACAO")).Should().Equal(Nomes(marca, "Renda familiar bruta", "Certidão de nascimento"));
        (await NomesAsync(client, Betina, $"busca={marca}&cadastro=TIPO_DOCUMENTO")).Should().Equal(Nomes(marca, "Laudo médico complementar", "Certidão de nascimento"));
        (await NomesAsync(client, Betina, $"busca={marca}&cadastro=FATO")).Should().Equal(Nomes(marca, "Renda familiar bruta"));
    }

    [Fact(DisplayName = "O filtro das próprias decisões compara quem consulta, e sem ele o Representante do controlador vê todas")]
    public async Task Decisoes_FiltroDasProprias()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        (await NomesAsync(client, Betina, $"busca={marca}&minhas=true")).Should().Equal(Nomes(marca, "Renda familiar bruta", "Certidão de nascimento"));
        (await NomesAsync(client, Caroline, $"busca={marca}&minhas=true")).Should().Equal(Nomes(marca, "Laudo médico complementar"));
        (await NomesAsync(client, Caroline, $"busca={marca}")).Should().HaveCount(3, "sem o filtro, o Representante do controlador vê todas as decisões");
    }

    [Fact(DisplayName = "O período restringe o recorte, e a data máxima não tem limite superior")]
    public async Task Decisoes_FiltroDePeriodo()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        DateOnly hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        (await NomesAsync(client, Betina, $"busca={marca}&de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}")).Should().HaveCount(3);
        (await NomesAsync(client, Betina, $"busca={marca}&de={hoje.AddDays(2):yyyy-MM-dd}")).Should().BeEmpty();
        (await NomesAsync(client, Betina, $"busca={marca}&ate={hoje.AddDays(-2):yyyy-MM-dd}")).Should().BeEmpty();
        (await NomesAsync(client, Betina, $"busca={marca}&ate=9999-12-31")).Should().HaveCount(3, "a data máxima não tem limite superior");
    }

    [Fact(DisplayName = "A decisão listada traz o cadastro, o ato, a classificação recomendada e o nome de quem decidiu, sem identificador da pessoa")]
    public async Task Decisoes_ItemDaLista()
    {
        (string marca, _, _, Guid ajustado) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        JsonElement ajuste = (await ListarDecisoesAsync(client, Betina, $"busca={marca}")).Single(d => d.GetProperty("itemId").GetGuid() == ajustado);

        (ajuste.GetProperty("cadastro").GetString(), ajuste.GetProperty("ato").GetString()).Should().Be(("TIPO_DOCUMENTO", "AJUSTE_RECOMENDADO"));
        (ajuste.GetProperty("classificacaoRecomendada").GetString(), ajuste.GetProperty("atorNome").GetString()).Should().Be(("SENSIVEL", Caroline.Nome));
        ajuste.EnumerateObject().Select(p => p.Name).Should().NotContain(["sub", "atorSub", "emissor", "atorEmissor"]);
    }

    [Theory(DisplayName = "A busca compara nome e código sem distinguir maiúsculas nem acentos")]
    [InlineData("LAUDO MEDICO")]
    [InlineData("laudo médico")]
    [InlineData("LAUDO MÉDICO")]
    public async Task Decisoes_BuscaSemAcentoNemCaixa(string trecho)
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        (await NomesAsync(client, Betina, $"busca={Uri.EscapeDataString($"{marca} {trecho}")}")).Should().Equal($"{marca} Laudo médico complementar");
        (await NomesAsync(client, Betina, $"busca={Uri.EscapeDataString(marca.ToUpperInvariant())}")).Should().HaveCount(3);
    }

    [Fact(DisplayName = "A paginação percorre as decisões em páginas sem repetir nem perder nenhuma")]
    public async Task Decisoes_Paginacao()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        (List<string> percorridos, int paginas) = await PercorrerAsync(client, $"{Base}/decisoes?busca={marca}&limit=2");

        percorridos.Should().Equal(Nomes(marca, "Laudo médico complementar", "Renda familiar bruta", "Certidão de nascimento"));
        paginas.Should().Be(2);
    }

    [Fact(DisplayName = "O cursor emitido para um recorte não continua outro")]
    public async Task Decisoes_CursorDeOutroRecorte_Retorna400()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        HttpResponseMessage primeira = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?busca={marca}&limit=1", Betina);
        string cursor = new Uri(LinkRel(primeira, "next")!, UriKind.RelativeOrAbsolute).Query.TrimStart('?').Split('&')
            .Single(p => p.StartsWith("cursor=", StringComparison.Ordinal));

        // O mesmo cursor, com o recorte trocado.
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?busca={marca}&cadastro=FATO&{cursor}&direction=next", Betina);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "O filtro fora do vocabulário, ou que não é decisão, e o período invertido respondem 422 no campo")]
    [InlineData("ato=SOLICITACAO", "ato_invalido")]
    [InlineData("ato=ACEITE", "ato_invalido")]
    [InlineData("cadastro=MODALIDADE", "cadastro_invalido")]
    [InlineData("de=2026-11-02&ate=2026-11-01", "periodo_invalido")]
    public async Task Decisoes_FiltroInvalido_Retorna422(string consulta, string codigo)
    {
        await PrepararPersonasAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?{consulta}", Betina);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(codigo);
    }

    [Fact(DisplayName = "A lista de decisões responde 403 sem a permissão de aprovar, mesmo a quem mantém os cadastros")]
    public async Task Decisoes_SemPermissaoDeAprovar_Retorna403()
    {
        const string Paulo = "paulo.conceicao";
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Paulo);
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            Fixture.Factory.Services, Paulo, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo,
            PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes", new Persona(Paulo, "Paulo"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string[] Nomes(string marca, params string[] nomes) => [.. nomes.Select(n => $"{marca} {n}")];

    private static async Task<(List<string> Percorridos, int Paginas)> PercorrerAsync(HttpClient client, string? url)
    {
        List<string> percorridos = [];
        int paginas = 0;
        while (url is not null)
        {
            HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, url, Betina);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            percorridos.AddRange((await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(d => d.GetProperty("itemNome").GetString()!));
            paginas++;
            url = LinkRel(resposta, "next");
        }

        return (percorridos, paginas);
    }
}
