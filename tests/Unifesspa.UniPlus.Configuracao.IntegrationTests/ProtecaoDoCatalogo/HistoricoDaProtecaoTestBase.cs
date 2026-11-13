namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O apoio dos testes do registro dos atos da proteção do catálogo: Isabella mantém e consulta os cadastros; Betina e
/// Caroline são Representantes do controlador, aprovam a proteção do catálogo e recebem o aviso da solicitação.
/// </summary>
public abstract class HistoricoDaProtecaoTestBase
{
    private protected const string Base = "/api/configuracao/admin/protecao-do-catalogo";
    private protected const string Tipos = "/api/configuracao/admin/tipos-documento";
    private protected const string Fatos = "/api/configuracao/admin/fatos-candidato";
    private protected const string Finalidade = "Comprovação documental do candidato.";

    private protected static readonly Persona Isabella = new("isabella.figueiredo", "Isabella Priscila Figueiredo");
    private protected static readonly Persona Betina = new("betina.neves", "Betina Marina Valentina das Neves");
    private protected static readonly Persona Caroline = new("caroline.lima", "Caroline Lima");

    private protected HistoricoDaProtecaoTestBase(ConfiguracaoEndpointFixture fixture) => Fixture = fixture;

    private protected ConfiguracaoEndpointFixture Fixture { get; }

    private protected static string Marca() => $"Hz{Guid.NewGuid().ToString("N")[..8]}";

    // Um tipo aprovado por Betina, um fato reprovado por Betina e um tipo com ajuste recomendado por Caroline, nessa ordem.
    private protected async Task<(string Marca, Guid Aprovado, Guid Reprovado, Guid Ajustado)> RegistrarTresDecisoesAsync()
    {
        (string marca, Guid aprovado, Guid reprovado, Guid ajustado) = await SolicitarTresAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        (await DecidirAsync(client, Betina, new("TIPO_DOCUMENTO", aprovado, "APROVAR", "INTERNO"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Task.Delay(20);
        (await DecidirAsync(client, Betina, new("FATO", reprovado, "REPROVAR", "PESSOAL", Justificativa: "Dado desnecessário."))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Task.Delay(20);
        (await DecidirAsync(client, Caroline, new("TIPO_DOCUMENTO", ajustado, "RECOMENDAR_AJUSTE", "INTERNO", "SENSIVEL"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return (marca, aprovado, reprovado, ajustado);
    }

    private protected async Task PrepararPersonasAsync()
    {
        IServiceProvider servicos = Fixture.Factory.Services;
        foreach (Persona persona in new[] { Isabella, Betina, Caroline })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, persona.Sub);
        }

        await ConcessoesDeTeste.ConcederPermissoesAsync(
            servicos, Isabella.Sub, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo, PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);
        await ConcessoesDeTeste.ConcederGovernancaAsync(servicos, Betina.Sub, TipoDeGovernanca.RepresentanteDoControlador, "betina.neves@unifesspa.edu.br");
        await ConcessoesDeTeste.ConcederGovernancaAsync(servicos, Caroline.Sub, TipoDeGovernanca.RepresentanteDoControlador, "caroline.lima@unifesspa.edu.br");
    }

    private protected static async Task<Guid> CadastrarTipoAsync(HttpClient client, string nome)
    {
        HttpResponseMessage criar = await Enviar(client, HttpMethod.Post, Tipos, Isabella, new
        {
            codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome,
            categoria = "SAUDE",
            classificacaoProtecao = "INTERNO",
            finalidadeTratamento = Finalidade,
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private protected static async Task<Guid> CadastrarFatoAsync(HttpClient client, string nome)
    {
        HttpResponseMessage criar = await Enviar(client, HttpMethod.Post, Fatos, Isabella, NovoFato(nome));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private protected static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, Persona quem, params (string Cadastro, Guid Id)[] itens) =>
        Enviar(client, HttpMethod.Post, $"{Base}/solicitacoes", quem, new { itens = itens.Select(i => new { cadastro = i.Cadastro, i.Id }) });

    private protected static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, Persona quem, string cadastro, Guid id) =>
        SolicitarAsync(client, quem, (cadastro, id));

    // A decisão leva o ciclo e a contagem de manifestações lidos no item; sem a leitura explícita, quem decide lê o
    // detalhe logo antes de enviar.
    private protected static async Task<HttpResponseMessage> DecidirAsync(HttpClient client, Persona quem, Decisao decisao) =>
        await Enviar(client, HttpMethod.Post, Base, quem, CorpoDaDecisao(
            decisao, decisao.Lida ?? await LerOCircuitoAsync(client, quem, decisao.Cadastro, decisao.ItemId)));

    /// <summary>O ciclo e a contagem de manifestações do item, como o detalhe da proteção os mostra a <paramref name="quem"/>.</summary>
    private protected static async Task<LeituraDoCircuitoLida> LerOCircuitoAsync(HttpClient client, Persona quem, string cadastro, Guid id)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/{(cadastro == "FATO" ? "fatos" : "tipos-documento")}/{id}", quem);
        JsonElement aprovacao = resposta.IsSuccessStatusCode
            ? (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("aprovacao")
            : default;
        return aprovacao.ValueKind == JsonValueKind.Object
            ? new(aprovacao.GetProperty("ciclo").GetInt32(), aprovacao.GetProperty("manifestacoesNoCiclo").GetInt32())
            : new(1, 0);
    }

    private protected static async Task<JsonElement> ObterFatoAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Fatos}/{id}", Isabella);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private protected static async Task<List<string>> NomesAsync(HttpClient client, Persona quem, string consulta) =>
        [.. (await ListarDecisoesAsync(client, quem, consulta)).Select(d => d.GetProperty("itemNome").GetString()!)];

    private protected static async Task<List<JsonElement>> ListarDecisoesAsync(HttpClient client, Persona quem, string consulta)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?{consulta}", quem);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-decisao.v1+json");
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private protected static string? LinkRel(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links))
        {
            return null;
        }

        string? parte = links.Single().Split(", ", StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(p => p.Contains($"rel=\"{rel}\"", StringComparison.Ordinal));
        return parte is null ? null : AlvoDoLink(parte);
    }

    private protected static async Task<HttpResponseMessage> Enviar(
        HttpClient client, HttpMethod metodo, string url, Persona quem, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.RelativeOrAbsolute));
        Autenticar(request, quem);
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        request.Content = corpo is null ? null : JsonContent.Create(corpo);
        return await client.SendAsync(request);
    }

    private async Task<(string Marca, Guid Aprovado, Guid Reprovado, Guid Ajustado)> SolicitarTresAsync()
    {
        await PrepararPersonasAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        string marca = Marca();
        Guid aprovado = await CadastrarTipoAsync(client, $"{marca} Certidão de nascimento");
        Guid reprovado = await CadastrarFatoAsync(client, $"{marca} Renda familiar bruta");
        Guid ajustado = await CadastrarTipoAsync(client, $"{marca} Laudo médico complementar");
        (await SolicitarAsync(client, Isabella, ("TIPO_DOCUMENTO", aprovado), ("FATO", reprovado), ("TIPO_DOCUMENTO", ajustado)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return (marca, aprovado, reprovado, ajustado);
    }

    private static object NovoFato(string nome) => new
    {
        codigo = $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
        nome,
        descricao = "Valor declarado pelo candidato.",
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = Finalidade,
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static string? AlvoDoLink(string parte)
    {
        int inicio = parte.IndexOf('<', StringComparison.Ordinal) + 1;
        int fim = parte.IndexOf('>', StringComparison.Ordinal);
        return inicio > 0 && fim > inicio ? parte[inicio..fim] : null;
    }

    private static void Autenticar(HttpRequestMessage request, Persona quem)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, quem.Sub);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.NameHeader, quem.Nome);
    }

    private protected sealed record Persona(string Sub, string Nome);

    private protected sealed record Decisao(
        string Cadastro, Guid ItemId, string Resultado, string Classificacao, string? Recomendada = null, string? Justificativa = null)
    {
        /// <summary>A leitura do circuito que a decisão leva; nula para ler o detalhe logo antes de enviar.</summary>
        public LeituraDoCircuitoLida? Lida { get; init; }
    }

    private protected sealed record LeituraDoCircuitoLida(int Ciclo, int Manifestacoes);

    private static object CorpoDaDecisao(Decisao decisao, LeituraDoCircuitoLida lida) => new
    {
        cadastro = decisao.Cadastro,
        itemId = decisao.ItemId,
        decisao = decisao.Resultado,
        classificacaoProtecao = decisao.Classificacao,
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        finalidadeTratamento = Finalidade,
        ciclo = lida.Ciclo,
        manifestacoesNoCiclo = lida.Manifestacoes,
        classificacaoRecomendada = decisao.Recomendada,
        justificativa = decisao.Justificativa,
    };
}
