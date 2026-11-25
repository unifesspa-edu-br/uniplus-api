namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.WebUtilities;

using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// <b>O 409 das rotas de publicação e retificação diz a causa, não o rótulo do status.</b>
/// </summary>
/// <remarks>
/// <para>
/// Declarar um status no atributo faz o gerador escrever a frase-padrão daquele status como
/// descrição — <c>"Conflict"</c> —, e uma palavra não diz ao cliente nem o que aconteceu nem o
/// que fazer. Nestas duas rotas o 409 tem causa própria: a corrida no congelamento da
/// versão. Quem gera cliente a partir do contrato não descobre nada disso num rótulo genérico.
/// </para>
/// <para>
/// A conferência é contra a <b>frase-padrão que o próprio framework produz</b>, e não contra a
/// palavra "Conflict" escrita aqui: é o mesmo texto que o gerador usa como default, então o teste
/// continua valendo se o framework mudar a frase — e não vira dependência de um literal em inglês
/// dentro do nosso código.
/// </para>
/// <para>
/// Cobre só estas duas. As outras rotas do monólito que declaram 409 seguem com o rótulo genérico
/// por decisão: descrever cada uma é trabalho de quem conhece a rota, e uma descrição errada é
/// pior que uma genérica.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class DescricaoDoConflitoNoContratoTests
{
    private const string Base = "/api/selecao/processos-seletivos/{id}";

    private static readonly (string Metodo, string Rota)[] RotasComConflitoDescrito =
    [
        ("post", $"{Base}/publicacao"),
        ("post", $"{Base}/retificacoes"),
    ];

    private readonly CascadingFixture _fixture;

    public DescricaoDoConflitoNoContratoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "As rotas de publicação e retificação descrevem a causa do 409, não o rótulo do status")]
    public async Task Contrato_QuandoDeclaraConflito_DeveDescreverACausa()
    {
        string generica = ReasonPhrases.GetReasonPhrase(409);

        using HttpClient client = _fixture.Factory.CreateClient();
        JsonElement documento = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/openapi/selecao.json", UriKind.Relative), CancellationToken.None);

        JsonElement paths = documento.GetProperty("paths");

        foreach ((string metodo, string rota) in RotasComConflitoDescrito)
        {
            ConferirDescricaoDoConflito(paths, metodo, rota, generica);
        }
    }

    private static void ConferirDescricaoDoConflito(JsonElement paths, string metodo, string rota, string generica)
    {
        paths.TryGetProperty(rota, out JsonElement caminho).Should().BeTrue(
            $"'{rota}' precisa existir no contrato para este teste dizer algo");

        JsonElement conflito = caminho.GetProperty(metodo).GetProperty("responses").GetProperty("409");
        string descricao = conflito.GetProperty("description").GetString()!;

        descricao.Should().NotBe(generica,
            $"o 409 de {metodo.ToUpperInvariant()} {rota} tem causa própria, e o rótulo do status não a diz");
        descricao.Length.Should().BeGreaterThan(generica.Length);

        conflito.TryGetProperty("content", out _).Should().BeTrue(
            "descrever a resposta não pode custar o corpo tipado que o cliente gerado usa para ler o erro");
    }
}
