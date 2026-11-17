namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System.Net;
using System.Net.Http;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>Formulário enviado ao token endpoint, validade do token em cache e recusa das respostas.</summary>
public sealed class OAuthBearerAuthenticationHeaderValueProviderRespostaTests : IDisposable
{
    private const string Segredo = "segredo-ficticio";
    private const string RespostaValida = """{"access_token":"jwt-ficticio","expires_in":300,"token_type":"Bearer"}""";

    private readonly StubHttpMessageHandler _manipulador = new();
    private readonly FakeTimeProvider _relogio = new();

    private static OAuthBearerSettings Configuracao(string? escopo) => new()
    {
        TokenEndpoint = "https://keycloak.exemplo.test/token",
        ClientId = "cliente-ficticio",
        ClientSecret = Segredo,
        Scope = escopo,
        RefreshSkewSeconds = 30,
    };

    private OAuthBearerAuthenticationHeaderValueProvider CriarProvedor(string? escopo = null) => new(
        new HttpClient(_manipulador),
        ownsHttpClient: true,
        Configuracao(escopo),
        NullLogger<OAuthBearerAuthenticationHeaderValueProvider>.Instance,
        _relogio);

    public void Dispose() => _manipulador.Dispose();

    private string FormularioEnviadoComEscopo(string escopo)
    {
        string formulario = string.Empty;
        _manipulador.EnqueueResponse(requisicao =>
        {
            formulario = requisicao.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RespostaValida) };
        });
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor(escopo);

        provedor.GetAuthenticationHeader();

        return formulario;
    }

    [Theory]
    [InlineData("""{"access_token":"","expires_in":300}""")]
    [InlineData("""{"access_token":"jwt-ficticio","expires_in":0}""")]
    [InlineData("null")]
    public void GetAuthenticationHeader_RespostaSemTokenOuSemValidade_LancaInvalidOperation(string corpo)
    {
        _manipulador.EnqueueResponse(HttpStatusCode.OK, corpo);
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor();

        Action obter = () => provedor.GetAuthenticationHeader();

        obter.Should().Throw<InvalidOperationException>()
            .WithMessage("Resposta do token endpoint inválida. ClientId=cliente-ficticio.");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void GetAuthenticationHeader_RespostaDeFalha_NaoRepeteOSegredoNaMensagem(HttpStatusCode status)
    {
        _manipulador.EnqueueResponse(status, "{}");
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor();

        Action obter = () => provedor.GetAuthenticationHeader();

        obter.Should().Throw<Exception>().Which.Message
            .Should().Contain("ClientId=cliente-ficticio").And.NotContain(Segredo);
    }

    [Fact]
    public void GetAuthenticationHeader_RespostaDeterministica_InformaOStatusNaMensagem()
    {
        _manipulador.EnqueueResponse(HttpStatusCode.Forbidden, "{}");
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor();

        Action obter = () => provedor.GetAuthenticationHeader();

        obter.Should().Throw<InvalidOperationException>().WithMessage("*status determinístico 403*");
    }

    [Fact]
    public void GetAuthenticationHeader_RespostaTransiente_InformaOStatusNaMensagem()
    {
        _manipulador.EnqueueResponse(HttpStatusCode.BadGateway, "{}");
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor();

        Action obter = () => provedor.GetAuthenticationHeader();

        obter.Should().Throw<HttpRequestException>().WithMessage("*status transiente 502*");
    }

    [Fact]
    public void GetAuthenticationHeader_EscopoInformado_EnviaOEscopoPorUltimoNoFormulario()
    {
        string formulario = FormularioEnviadoComEscopo("openid");

        formulario.Should().Be($"grant_type=client_credentials&client_id=cliente-ficticio&client_secret={Segredo}&scope=openid");
    }

    [Fact]
    public void GetAuthenticationHeader_EscopoEmBranco_NaoEnviaOEscopo()
    {
        string formulario = FormularioEnviadoComEscopo("  ");

        formulario.Should().Be($"grant_type=client_credentials&client_id=cliente-ficticio&client_secret={Segredo}");
    }

    [Fact]
    public void GetAuthenticationHeader_UmSegundoAntesDaMargem_ReusaOTokenEmCache()
    {
        _manipulador.EnqueueResponse(HttpStatusCode.OK, RespostaValida);
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor();
        provedor.GetAuthenticationHeader();

        _relogio.Advance(TimeSpan.FromSeconds(269));
        provedor.GetAuthenticationHeader();

        _manipulador.CallCount.Should().Be(1);
    }

    [Fact]
    public void GetAuthenticationHeader_NoInstanteExatoDaMargem_RenovaOToken()
    {
        _manipulador.EnqueueResponse(HttpStatusCode.OK, RespostaValida);
        _manipulador.EnqueueResponse(HttpStatusCode.OK, RespostaValida);
        using OAuthBearerAuthenticationHeaderValueProvider provedor = CriarProvedor();
        provedor.GetAuthenticationHeader();

        _relogio.Advance(TimeSpan.FromSeconds(270));
        provedor.GetAuthenticationHeader();

        _manipulador.CallCount.Should().Be(2);
    }
}
