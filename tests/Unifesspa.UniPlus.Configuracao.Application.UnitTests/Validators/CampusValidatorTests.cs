namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

/// <summary>
/// S2 (#587): o validator antecipa formato de CEP e coordenadas (além de
/// sigla/nome/cidade), mantendo a fronteira de validação simétrica com o domínio.
/// </summary>
public sealed class CampusValidatorTests
{
    private readonly CriarCampusCommandValidator _validator = new();

    private static CriarCampusCommand Base() =>
        new("CAMar", "Campus Marabá", "1504208", "Marabá", "PA", null, null, null, null, null);

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base() with { Cep = "68507590", Latitude = -5.3m, Longitude = -49.1m })
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "CEP em formato inválido é rejeitado pelo validator")]
    [InlineData("6850-759")]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    public void CepInvalido_Rejeita(string cep)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Cep = cep });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarCampusCommand.Cep));
    }

    [Fact(DisplayName = "CEP ausente é aceito (campo opcional)")]
    public void CepAusente_Aceita()
    {
        _validator.Validate(Base() with { Cep = null }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Coordenada fora de faixa é rejeitada pelo validator")]
    [InlineData(-90.1, 0, nameof(CriarCampusCommand.Latitude))]
    [InlineData(90.1, 0, nameof(CriarCampusCommand.Latitude))]
    [InlineData(0, -180.1, nameof(CriarCampusCommand.Longitude))]
    [InlineData(0, 180.1, nameof(CriarCampusCommand.Longitude))]
    public void CoordenadaForaDeFaixa_Rejeita(double latitude, double longitude, string propriedade)
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { Latitude = (decimal)latitude, Longitude = (decimal)longitude });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == propriedade);
    }
}
