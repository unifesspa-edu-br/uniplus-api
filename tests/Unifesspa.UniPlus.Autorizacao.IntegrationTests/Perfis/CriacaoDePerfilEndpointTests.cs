namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class CriacaoDePerfilEndpointTests : IClassFixture<CenarioDeCriacaoDePerfil>
{
    private readonly CenarioDeCriacaoDePerfil _cenario;

    public CriacaoDePerfilEndpointTests(CenarioDeCriacaoDePerfil cenario) => _cenario = cenario;

    [Fact(DisplayName = "Criar perfil devolve a URL de envio do documento")]
    public void Criar_Valido_DevolveEnvio()
    {
        _cenario.Status.Should().Be(HttpStatusCode.Created);
        _cenario.RespostaDeEnvio.GetProperty("situacao").GetString().Should().Be("aguardando_envio");
        _cenario.RespostaDeEnvio.GetProperty("tipoDeMidia").GetString().Should().Be("application/pdf");
        _cenario.RespostaDeEnvio.GetProperty("urlDeEnvio").GetString().Should().StartWith("http");
    }

    [Fact(DisplayName = "O perfil criado nasce ativo e sem composição em vigor")]
    public void Criar_Valido_NasceAtivoSemComposicao()
    {
        _cenario.Perfil.GetProperty("ativo").GetBoolean().Should().BeTrue();
        _cenario.Perfil.GetProperty("governanca").ValueKind.Should().Be(JsonValueKind.Null);
        _cenario.Perfil.GetProperty("composicaoEmVigor").ValueKind.Should().Be(JsonValueKind.Null);
        _cenario.Perfil.GetProperty("sensibilidade").GetString().Should().Be("INTERNO");
    }

    [Fact(DisplayName = "O perfil criado tem versão pendente com as permissões propostas")]
    public void Criar_Valido_NasceComVersaoPendente()
    {
        JsonElement pendente = _cenario.Perfil.GetProperty("versaoPendente");
        pendente.GetProperty("numero").GetInt32().Should().Be(1);
        pendente.GetProperty("situacaoDoDocumento").GetString().Should().Be("aguardando_envio");
        pendente.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString())
            .Should().Equal(PerfilEndpointTestBase.ConsultarCatalogo, PerfilEndpointTestBase.ManterMotivos);
    }
}

public sealed class CenarioDeCriacaoDePerfil : PerfilEndpointTestBase, IAsyncLifetime
{
    public CenarioDeCriacaoDePerfil(AutorizacaoEndpointFixture fixture) : base(fixture)
    {
    }

    public HttpStatusCode Status { get; private set; }
    public JsonElement RespostaDeEnvio { get; private set; }
    public JsonElement Perfil { get; private set; }

    public async Task InitializeAsync()
    {
        using HttpClient cliente = await ClienteDoGestorAsync();
        using HttpResponseMessage resposta = await EnviarAsync(cliente, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo, ManterMotivos]));
        Status = resposta.StatusCode;
        using JsonDocument envio = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        RespostaDeEnvio = envio.RootElement.Clone();
        using JsonDocument perfil = await LerAsync(cliente, RespostaDeEnvio.GetProperty("perfilId").GetGuid());
        Perfil = perfil.RootElement.Clone();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
