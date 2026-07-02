namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

/// <summary>
/// O validator antecipa o formato do código, a pertença ao conjunto canônico das
/// quatro bancas e os tamanhos. A fase típica é orientativa (não validada contra o
/// cadastro de fases), apenas limitada em tamanho.
/// </summary>
public sealed class TipoBancaValidatorTests
{
    private readonly CriarTipoBancaCommandValidator _criarValidator = new();
    private readonly AtualizarTipoBancaCommandValidator _atualizarValidator = new();

    private static CriarTipoBancaCommand Base() =>
        new("BANCA_ENTREVISTA", Nome: "Banca de entrevista");

    [Fact(DisplayName = "Comando válido passa no validator de criação")]
    public void Criar_Valido_Passa()
    {
        _criarValidator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando válido sem fase típica passa")]
    public void Criar_SemFaseTipica_Passa()
    {
        _criarValidator.Validate(Base() with { FaseTipica = null }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código fora do formato é rejeitado")]
    [InlineData("banca_entrevista")]
    [InlineData("BANCA-ENTREVISTA")]
    [InlineData("")]
    public void Criar_CodigoInvalido_Rejeita(string codigo)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoBancaCommand.Codigo));
    }

    [Fact(DisplayName = "Código bem-formado fora do conjunto canônico é rejeitado")]
    public void Criar_CodigoForaDoCanonico_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Codigo = "BANCA_LOGISTICA" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoBancaCommand.Codigo));
    }

    [Fact(DisplayName = "Código canônico com espaços ao redor passa (validator trima como TipoBanca.Criar)")]
    public void Criar_CodigoCanonicoComEspacos_Passa()
    {
        _criarValidator.Validate(Base() with { Codigo = " BANCA_ENTREVISTA " }).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void Criar_SemNome_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Nome = "" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoBancaCommand.Nome));
    }

    [Fact(DisplayName = "Fase típica acima de 60 caracteres é rejeitada")]
    public void Criar_FaseTipicaLonga_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { FaseTipica = new string('a', 61) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoBancaCommand.FaseTipica));
    }

    [Fact(DisplayName = "Atualização com Id vazio é rejeitada")]
    public void Atualizar_IdVazio_Rejeita()
    {
        ValidationResult resultado = _atualizarValidator.Validate(
            new AtualizarTipoBancaCommand(Guid.Empty, Nome: "Banca de entrevista"));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarTipoBancaCommand.Id));
    }

    [Fact(DisplayName = "Atualização válida passa no validator")]
    public void Atualizar_Valido_Passa()
    {
        _atualizarValidator.Validate(
            new AtualizarTipoBancaCommand(Guid.CreateVersion7(), Nome: "Banca de entrevista"))
            .IsValid.Should().BeTrue();
    }
}
