namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>
/// A leitura da regra efetiva do subject: sempre com <c>defaultToGlobal=true</c>, com a URL e a autenticação das opções
/// do Schema Registry, e sem aceitar resposta que não dê para entender.
/// </summary>
public sealed class LeitorDaRegraDeCompatibilidadeDoSubjectTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly HttpClient _http;

    public LeitorDaRegraDeCompatibilidadeDoSubjectTests() => _http = new HttpClient(_handler);

    public void Dispose() => _http.Dispose();

    [Fact(DisplayName = "Lê a regra efetiva do subject pedindo defaultToGlobal=true, com o subject escapado")]
    public async Task LeComDefaultToGlobal()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"compatibilityLevel":"NONE"}""");

        Compatibility regra = await Leitor().LerRegraDeCompatibilidadeEfetivaAsync("documento v1/ç", default);

        regra.Should().Be(Compatibility.None);
        _handler.ReceivedRequests.Single().RequestUri!.AbsoluteUri
            .Should().Be("http://registry:8081/apis/ccompat/v7/config/documento%20v1%2F%C3%A7?defaultToGlobal=true");
    }

    [Theory(DisplayName = "Entende os níveis do Confluent, inclusive os com sublinhado")]
    [InlineData("BACKWARD", Compatibility.Backward)]
    [InlineData("BACKWARD_TRANSITIVE", Compatibility.BackwardTransitive)]
    [InlineData("FULL_TRANSITIVE", Compatibility.FullTransitive)]
    [InlineData("none", Compatibility.None)]
    public async Task EntendeOsNiveis(string nivel, Compatibility esperada)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, $$"""{"compatibilityLevel":"{{nivel}}"}""");

        (await Leitor().LerRegraDeCompatibilidadeEfetivaAsync("s", default)).Should().Be(esperada);
    }

    [Theory(DisplayName = "Resposta sem nível ou com nível desconhecido é falha determinística")]
    [InlineData("{}")]
    [InlineData("""{"compatibilityLevel":"INVENTADA"}""")]
    public async Task RespostaIlegivel_Falha(string corpo)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, corpo);

        Func<Task> acao = () => Leitor().LerRegraDeCompatibilidadeEfetivaAsync("s", default);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Subject que ainda não existe (404) tem como regra efetiva a global")]
    public async Task SubjectInexistente_LeAGlobal()
    {
        _handler.EnqueueResponse(HttpStatusCode.NotFound, """{"error_code":40401}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"compatibilityLevel":"FULL"}""");

        Compatibility regra = await Leitor().LerRegraDeCompatibilidadeEfetivaAsync("novo", default);

        regra.Should().Be(Compatibility.Full);
        _handler.ReceivedRequests.Select(r => r.RequestUri!.AbsoluteUri).Should().Equal(
            "http://registry:8081/apis/ccompat/v7/config/novo?defaultToGlobal=true",
            "http://registry:8081/apis/ccompat/v7/config");
    }

    [Fact(DisplayName = "Sem a regra global (404 também nela), a leitura falha")]
    public async Task SemGlobal_Falha()
    {
        _handler.EnqueueResponse(HttpStatusCode.NotFound);
        _handler.EnqueueResponse(HttpStatusCode.NotFound);

        Func<Task> acao = () => Leitor().LerRegraDeCompatibilidadeEfetivaAsync("novo", default);

        await acao.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory(DisplayName = "Resposta de erro do Schema Registry é falha determinística")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task RespostaDeErro_Falha(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status);

        Func<Task> acao = () => Leitor().LerRegraDeCompatibilidadeEfetivaAsync("s", default);

        (await acao.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
    }

    [Fact(DisplayName = "Com credencial básica nas opções, envia o cabeçalho Basic")]
    public async Task ComCredencialBasica_EnviaBasic()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"compatibilityLevel":"NONE"}""");

        await Leitor(basicAuthUserInfo: "usuario:senha").LerRegraDeCompatibilidadeEfetivaAsync("s", default);

        AuthenticationHeaderValue cabecalho = _handler.ReceivedRequests.Single().Headers.Authorization!;
        cabecalho.Scheme.Should().Be("Basic");
        cabecalho.Parameter.Should().Be(Convert.ToBase64String("usuario:senha"u8.ToArray()));
    }

    [Fact(DisplayName = "Com o provider OAuth do Schema Registry, envia o cabeçalho que ele dá")]
    public async Task ComOAuth_EnviaOCabecalhoDoProvider()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"compatibilityLevel":"NONE"}""");
        IAuthenticationHeaderValueProvider provider = Substitute.For<IAuthenticationHeaderValueProvider>();
        provider.GetAuthenticationHeader().Returns(new AuthenticationHeaderValue("Bearer", "token-de-teste"));

        await Leitor(oauth: provider).LerRegraDeCompatibilidadeEfetivaAsync("s", default);

        _handler.ReceivedRequests.Single().Headers.Authorization!.Parameter.Should().Be("token-de-teste");
    }

    [Fact(DisplayName = "Sem credencial nas opções, não envia cabeçalho de autenticação")]
    public async Task SemCredencial_NaoEnviaAutenticacao()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"compatibilityLevel":"NONE"}""");

        await Leitor().LerRegraDeCompatibilidadeEfetivaAsync("s", default);

        _handler.ReceivedRequests.Single().Headers.Authorization.Should().BeNull();
    }

    private LeitorDaRegraDeCompatibilidadeDoSubject Leitor(string? basicAuthUserInfo = null, IAuthenticationHeaderValueProvider? oauth = null) =>
        new(_http, Options.Create(new SchemaRegistrySettings { Url = "http://registry:8081/apis/ccompat/v7/", BasicAuthUserInfo = basicAuthUserInfo }), oauth);
}
