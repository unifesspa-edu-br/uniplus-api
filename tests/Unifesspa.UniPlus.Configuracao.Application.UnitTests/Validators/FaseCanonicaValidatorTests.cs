namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

/// <summary>
/// O validator antecipa o formato do código, a pertença ao conjunto canônico, o
/// domínio fechado do dono típico e os tamanhos. A coerência
/// <c>agrupa_etapas</c>/<c>permite_complementacao</c> (que depende do código) fica
/// no agregado.
/// </summary>
public sealed class FaseCanonicaValidatorTests
{
    private readonly CriarFaseCanonicaCommandValidator _criarValidator = new();
    private readonly AtualizarFaseCanonicaCommandValidator _atualizarValidator = new();

    private static CriarFaseCanonicaCommand Base() =>
        new("INSCRICAO", Nome: "Inscrição", DonoTipico: "CEPS");

    [Fact(DisplayName = "Comando válido passa no validator de criação")]
    public void Criar_Valido_Passa()
    {
        _criarValidator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código fora do formato é rejeitado")]
    [InlineData("inscricao")]
    [InlineData("RESULTADO-FINAL")]
    [InlineData("")]
    public void Criar_CodigoInvalido_Rejeita(string codigo)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarFaseCanonicaCommand.Codigo));
    }

    [Fact(DisplayName = "Código bem-formado fora do conjunto canônico é rejeitado")]
    public void Criar_CodigoForaDoCanonico_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Codigo = "ENTREVISTA_FINAL" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarFaseCanonicaCommand.Codigo));
    }

    [Theory(DisplayName = "Dono típico fora do domínio (incl. numérico e PascalCase) é rejeitado")]
    [InlineData("DTI")]
    [InlineData("1")]
    [InlineData("Ceps")]
    public void Criar_DonoTipicoInvalido_Rejeita(string dono)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { DonoTipico = dono });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarFaseCanonicaCommand.DonoTipico));
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void Criar_SemNome_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Nome = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarFaseCanonicaCommand.Nome));
    }

    [Fact(DisplayName = "Dono típico ausente é rejeitado")]
    public void Criar_SemDonoTipico_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { DonoTipico = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarFaseCanonicaCommand.DonoTipico));
    }

    [Fact(DisplayName = "Atualização com Id vazio é rejeitada")]
    public void Atualizar_IdVazio_Rejeita()
    {
        ValidationResult resultado = _atualizarValidator.Validate(
            new AtualizarFaseCanonicaCommand(Guid.Empty, Nome: "Inscrição", DonoTipico: "CEPS"));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarFaseCanonicaCommand.Id));
    }

    [Fact(DisplayName = "Atualização válida passa no validator")]
    public void Atualizar_Valido_Passa()
    {
        _atualizarValidator.Validate(
            new AtualizarFaseCanonicaCommand(Guid.CreateVersion7(), Nome: "Inscrição", DonoTipico: "CEPS"))
            .IsValid.Should().BeTrue();
    }
}
