namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Geo;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

/// <summary>
/// O endereço do Geo é obrigatório e precisa ser http ou https absoluto: o valor mal formado barra
/// o startup em vez de chegar à conferência do endereço do candidato como erro inesperado.
/// </summary>
public sealed class GeoOptionsValidacaoTests
{
    [Theory(DisplayName = "Geo:BaseUrl que não é URI http ou https absoluta é recusado na validação das opções")]
    [InlineData("geo-api:8080")]
    [InlineData("/api/geo")]
    [InlineData("ftp://geo.exemplo.test")]
    [InlineData("http//geo-api")]
    public void BaseUrl_ForaDoFormato_Recusa(string endereco)
    {
        Action ler = () => Opcoes(endereco);

        ler.Should().Throw<OptionsValidationException>().WithMessage("*Geo:BaseUrl*");
    }

    [Theory(DisplayName = "Geo:BaseUrl http ou https absoluto é aceito")]
    [InlineData("http://localhost:5400")]
    [InlineData("http://geo-api:8080")]
    [InlineData("https://geo.exemplo.test")]
    public void BaseUrl_Absoluta_Aceita(string endereco) =>
        Opcoes(endereco).BaseUrl.Should().Be(endereco);

    private static GeoOptions Opcoes(string endereco)
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Geo:BaseUrl"] = endereco })
            .Build();

        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddSingleton(configuracao);
        servicos.AddGeoClient();

        using ServiceProvider provedor = servicos.BuildServiceProvider();
        return provedor.GetRequiredService<IOptions<GeoOptions>>().Value;
    }
}
