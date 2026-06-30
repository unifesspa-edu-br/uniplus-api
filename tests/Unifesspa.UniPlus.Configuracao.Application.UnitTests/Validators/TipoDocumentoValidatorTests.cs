namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

/// <summary>
/// O validator antecipa a obrigatoriedade de código e nome, o domínio fechado da
/// categoria, a positividade do tamanho máximo e o guard de auto-equivalência,
/// mantendo a fronteira simétrica com o domínio (#591).
/// </summary>
public sealed class TipoDocumentoValidatorTests
{
    private readonly CriarTipoDocumentoCommandValidator _validator = new();

    private static CriarTipoDocumentoCommand Base() =>
        new("LAUDO_MEDICO", "Laudo médico", "SAUDE", "Documento de saúde", "pdf,jpg", 10, null);

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando sem campos opcionais passa no validator")]
    public void SemOpcionais_Passa()
    {
        _validator.Validate(Base() with { Descricao = null, FormatosAceitos = null, TamanhoMaximoMb = null, TipoEquivalente = null })
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void CodigoVazio_Rejeita(string codigo)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDocumentoCommand.Codigo));
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void NomeVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { Nome = "  " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDocumentoCommand.Nome));
    }

    [Theory(DisplayName = "Categoria fora do domínio fechado é rejeitada (incl. numérico e PascalCase)")]
    [InlineData("FINANCEIRO")]
    [InlineData("1")]
    [InlineData("Saude")]
    public void CategoriaInvalida_Rejeita(string categoria)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Categoria = categoria });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDocumentoCommand.Categoria));
    }

    [Theory(DisplayName = "Tamanho máximo zero ou negativo é rejeitado")]
    [InlineData(0)]
    [InlineData(-5)]
    public void TamanhoMaximoNaoPositivo_Rejeita(int mb)
    {
        ValidationResult resultado = _validator.Validate(Base() with { TamanhoMaximoMb = mb });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoDocumentoCommand.TamanhoMaximoMb));
    }

    [Fact(DisplayName = "Tipo equivalente igual ao próprio código é rejeitado")]
    public void EquivalenteIgualCodigo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { Codigo = "RG", Categoria = "IDENTIFICACAO", TipoEquivalente = "RG" });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Tipo equivalente apontando para outro código é aceito")]
    public void EquivalenteOutroCodigo_Aceita()
    {
        _validator.Validate(Base() with { Codigo = "RG", Categoria = "IDENTIFICACAO", TipoEquivalente = "CIN" })
            .IsValid.Should().BeTrue();
    }
}
