namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Geo;

using System.Net.Http;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

/// <summary>
/// <see cref="GeoTokenProvider"/>: a seção <c>Geo:OAuth</c> é opcional na configuração, então a
/// configuração ausente ou inválida precisa surgir como <see cref="GeoAutenticacaoException"/> — a
/// única exceção de autenticação que a conferência traduz em falha fechada — e nunca como
/// <see cref="UriFormatException"/> ou como uma chamada de rede com credencial vazia.
/// </summary>
public sealed class GeoTokenProviderTests
{
    [Theory(DisplayName = "Configuração OAuth ausente ou inválida lança GeoAutenticacaoException antes de qualquer chamada de rede")]
    [InlineData("", "cliente", "segredo")]
    [InlineData("   ", "cliente", "segredo")]
    [InlineData("/realms/unifesspa/token", "cliente", "segredo")]
    [InlineData("ftp://keycloak.exemplo.test/token", "cliente", "segredo")]
    [InlineData("https://keycloak.exemplo.test/token", "", "segredo")]
    [InlineData("https://keycloak.exemplo.test/token", "cliente", "")]
    public async Task Obter_ConfiguracaoOAuthInvalida_LancaGeoAutenticacaoException(
        string tokenEndpoint, string clientId, string clientSecret)
    {
        IHttpClientFactory clientes = Substitute.For<IHttpClientFactory>();
        GeoOptions opcoes = new()
        {
            BaseUrl = "https://geo.exemplo.test",
            OAuth = new GeoOAuthOptions { TokenEndpoint = tokenEndpoint, ClientId = clientId, ClientSecret = clientSecret },
        };

        using GeoTokenProvider provedor = new(
            clientes, Options.Create(opcoes), NullLogger<GeoTokenProvider>.Instance, TimeProvider.System);

        Func<Task> obter = () => provedor.ObterAsync(CancellationToken.None);

        await obter.Should().ThrowAsync<GeoAutenticacaoException>();
        clientes.ReceivedCalls().Should().BeEmpty();
    }
}
