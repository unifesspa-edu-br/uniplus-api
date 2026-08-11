namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

public sealed class TipoEtapaValidatorTests
{
    private readonly CriarTipoEtapaCommandValidator _criarValidator = new();
    private readonly AtualizarTipoEtapaCommandValidator _atualizarValidator = new();

    [Theory(DisplayName = "Validator de criação recusa U+0000 em todos os campos textuais")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public void Criar_CampoComCaractereNulo_Recusa(string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        CriarTipoEtapaCommand command = campo switch
        {
            "codigo" => new(invalido, "Nome válido", "Descrição válida"),
            "nome" => new("CODIGO_VALIDO", invalido, "Descrição válida"),
            "descricao" => new("CODIGO_VALIDO", "Nome válido", invalido),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        ValidationResult result = _criarValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => string.Equals(error.PropertyName, campo, StringComparison.OrdinalIgnoreCase));
    }

    [Theory(DisplayName = "Validator de atualização recusa U+0000 nos campos editáveis")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public void Atualizar_CampoComCaractereNulo_Recusa(string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        AtualizarTipoEtapaCommand command = campo switch
        {
            "nome" => new(Guid.CreateVersion7(), invalido, "Descrição válida"),
            "descricao" => new(Guid.CreateVersion7(), "Nome válido", invalido),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        ValidationResult result = _atualizarValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => string.Equals(error.PropertyName, campo, StringComparison.OrdinalIgnoreCase));
    }
}
