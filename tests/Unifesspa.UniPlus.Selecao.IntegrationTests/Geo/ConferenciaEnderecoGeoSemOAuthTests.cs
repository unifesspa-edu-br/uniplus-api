namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Geo;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

/// <summary>
/// A seção <c>Geo:OAuth</c> é opcional na configuração (não derruba o startup): a ausência do
/// endpoint só aparece quando alguém chama o Geo, e precisa ser falha fechada. Usa o registro real
/// (<see cref="GeoClientRegistration.AddGeoClient"/>) — é a criação do cliente HTTP de autenticação
/// e a montagem da URI do token que lançavam.
/// </summary>
public sealed class ConferenciaEnderecoGeoSemOAuthTests
{
    [Fact(DisplayName = "Sem endpoint OAuth configurado, a conferência no Geo é falha fechada, não exceção")]
    public async Task Conferir_SemTokenEndpoint_FalhaFechada()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Geo:BaseUrl"] = "https://geo.exemplo.test" })
            .Build();

        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddSingleton(configuracao);
        servicos.AddGeoClient();

        await using ServiceProvider provedor = servicos.BuildServiceProvider();
        await using AsyncServiceScope escopo = provedor.CreateAsyncScope();
        IConferenciaEnderecoGeo conferencia = escopo.ServiceProvider.GetRequiredService<IConferenciaEnderecoGeo>();

        Result cep = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");
        Result municipio = await conferencia.ConferirMunicipioAsync("1504208", "PA");

        cep.IsFailure.Should().BeTrue();
        cep.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        municipio.IsFailure.Should().BeTrue();
        municipio.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }
}
