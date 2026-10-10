namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Geo;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O dublê de <see cref="IConferenciaEnderecoGeo"/> (issue #1865) que a issue #1859 vai usar para
/// validar a submissão da inscrição sem depender da stack local. Prova aqui que ele se comporta
/// como o Geo real nos quatro casos que a validação dinâmica precisa distinguir.
/// </summary>
public sealed class FakeConferenciaEnderecoGeoTests
{
    [Fact(DisplayName = "CEP não registrado no dublê não resolve — mesmo comportamento de CEP sem cobertura no Geo real")]
    public async Task ConferirCep_NaoRegistrado_NaoResolve()
    {
        FakeConferenciaEnderecoGeo conferencia = new();

        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve);
    }

    [Fact(DisplayName = "CEP registrado confere quando a cidade e a UF declaradas batem o que o dublê conhece")]
    public async Task ConferirCep_Registrado_Confere()
    {
        FakeConferenciaEnderecoGeo conferencia = new FakeConferenciaEnderecoGeo()
            .ComCep("68500000", "1504208", "PA");

        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Município registrado numa UF diferente da declarada diverge")]
    public async Task ConferirMunicipio_UfDiferente_Diverge()
    {
        FakeConferenciaEnderecoGeo conferencia = new FakeConferenciaEnderecoGeo()
            .ComMunicipio("1504208", "PA");

        Result resultado = await conferencia.ConferirMunicipioAsync("1504208", "SP");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente);
    }

    [Fact(DisplayName = "Indisponivel() faz toda chamada falhar fechada, mesmo para um CEP registrado")]
    public async Task Indisponivel_FalhaFechadaMesmoComCepRegistrado()
    {
        FakeConferenciaEnderecoGeo conferencia = new FakeConferenciaEnderecoGeo()
            .ComCep("68500000", "1504208", "PA")
            .Indisponivel();

        Result resultado = await conferencia.ConferirCepAsync("68500000", "1504208", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }
}
