namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

/// <summary>
/// O validator antecipa o formato dos códigos. As guardas que dependem do grafo
/// vigente (self-loop, aresta duplicada, ciclo) ficam no agregado.
/// </summary>
public sealed class PrecedenciaFaseValidatorTests
{
    private readonly CriarPrecedenciaFaseCommandValidator _criarValidator = new();
    private readonly AtualizarPrecedenciaFaseCommandValidator _atualizarValidator = new();

    private static CriarPrecedenciaFaseCommand Base() =>
        new("INSCRICAO", "HOMOLOGACAO");

    [Fact(DisplayName = "Comando válido passa no validator de criação")]
    public void Criar_Valido_Passa()
    {
        _criarValidator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código de antecessora fora do formato é rejeitado")]
    [InlineData("inscricao")]
    [InlineData("")]
    public void Criar_AntecessoraInvalida_Rejeita(string antecessora)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { AntecessoraCodigo = antecessora });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarPrecedenciaFaseCommand.AntecessoraCodigo));
    }

    [Theory(DisplayName = "Código de sucessora fora do formato é rejeitado")]
    [InlineData("homologacao")]
    [InlineData("")]
    public void Criar_SucessoraInvalida_Rejeita(string sucessora)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { SucessoraCodigo = sucessora });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarPrecedenciaFaseCommand.SucessoraCodigo));
    }

    [Fact(DisplayName = "Atualização com Id vazio é rejeitada")]
    public void Atualizar_IdVazio_Rejeita()
    {
        ValidationResult resultado = _atualizarValidator.Validate(
            new AtualizarPrecedenciaFaseCommand(Guid.Empty, true));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarPrecedenciaFaseCommand.Id));
    }

    [Fact(DisplayName = "Atualização válida passa no validator")]
    public void Atualizar_Valido_Passa()
    {
        _atualizarValidator.Validate(new AtualizarPrecedenciaFaseCommand(Guid.CreateVersion7(), true))
            .IsValid.Should().BeTrue();
    }
}
