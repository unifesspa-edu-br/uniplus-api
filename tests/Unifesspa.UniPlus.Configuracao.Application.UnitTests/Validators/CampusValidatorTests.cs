namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// CA-03/CA-04 (#726): o validator antecipa o formato do endereço estruturado e a
/// coerência cidade↔CEP (além de sigla/nome/cidade), mantendo a fronteira de
/// validação simétrica com o domínio.
/// </summary>
public sealed class CampusValidatorTests
{
    private readonly CriarCampusCommandValidator _validator = new();

    private static CriarCampusCommand Base() =>
        new("CAMar", "Campus Marabá", "1504208", "Marabá", "PA", null, null);

    private static EnderecoGeoInput EnderecoValido(string cidadeCodigoIbge = "1504208", string? cep = "68507590") =>
        new(cep, "Folha 31", "s/n", null, "Nova Marabá", null,
            new CidadeReferenciaInput(cidadeCodigoIbge, "Marabá", "PA"),
            -5.3m, -49.1m, NivelResolucaoEndereco.Logradouro, "logradouro");

    [Fact(DisplayName = "Comando sem endereço passa no validator")]
    public void SemEndereco_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando com endereço estruturado válido passa")]
    public void EnderecoValido_Passa()
    {
        _validator.Validate(Base() with { Endereco = EnderecoValido() }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Endereço com CEP em formato inválido é rejeitado")]
    [InlineData("6850-759")]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    public void EnderecoCepInvalido_Rejeita(string cep)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Endereco = EnderecoValido(cep: cep) });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Endereço com cidade incoerente com a cidade do campus é rejeitado (CA-04)")]
    public void EnderecoCidadeIncoerente_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { Endereco = EnderecoValido(cidadeCodigoIbge: "1501402") });

        resultado.IsValid.Should().BeFalse();
    }
}
