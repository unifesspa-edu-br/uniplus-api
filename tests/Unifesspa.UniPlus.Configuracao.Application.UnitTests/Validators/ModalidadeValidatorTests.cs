namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

/// <summary>
/// O validator antecipa o formato do código, o domínio fechado dos enums e os
/// tamanhos, mantendo a fronteira simétrica com o domínio (#589). A coerência e a
/// integridade referencial ficam no agregado e no handler.
/// </summary>
public sealed class ModalidadeValidatorTests
{
    private readonly CriarModalidadeCommandValidator _criarValidator = new();
    private readonly AtualizarModalidadeCommandValidator _atualizarValidator = new();

    private static CriarModalidadeCommand Base() =>
        new("AC", Descricao: "Ampla concorrência", NaturezaLegal: "AMPLA", ComposicaoVagas: "RESIDUAL_DO_VO");

    [Fact(DisplayName = "Comando válido passa no validator de criação")]
    public void Criar_Valido_Passa()
    {
        _criarValidator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando sem enums opcionais (defaults) passa")]
    public void Criar_SemEnumsOpcionais_Passa()
    {
        _criarValidator.Validate(new CriarModalidadeCommand("AC")).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Código fora do formato é rejeitado")]
    [InlineData("lb_ppi")]
    [InlineData("LB-PPI")]
    [InlineData("")]
    public void Criar_CodigoInvalido_Rejeita(string codigo)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarModalidadeCommand.Codigo));
    }

    [Theory(DisplayName = "Natureza legal fora do domínio é rejeitada (incl. numérico e PascalCase)")]
    [InlineData("COTA")]
    [InlineData("1")]
    [InlineData("Ampla")]
    public void Criar_NaturezaInvalida_Rejeita(string natureza)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { NaturezaLegal = natureza });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarModalidadeCommand.NaturezaLegal));
    }

    [Theory(DisplayName = "Composição de vagas fora do domínio é rejeitada")]
    [InlineData("RESIDUAL")]
    [InlineData("2")]
    public void Criar_ComposicaoInvalida_Rejeita(string composicao)
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { ComposicaoVagas = composicao });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarModalidadeCommand.ComposicaoVagas));
    }

    [Fact(DisplayName = "Regra de remanejamento fora do domínio é rejeitada")]
    public void Criar_RegraInvalida_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { RegraRemanejamento = "CASCATA" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarModalidadeCommand.RegraRemanejamento));
    }

    [Fact(DisplayName = "Ação quando indeferido fora do domínio é rejeitada")]
    public void Criar_AcaoInvalida_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { AcaoQuandoIndeferido = "REPROVAR" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarModalidadeCommand.AcaoQuandoIndeferido));
    }

    [Fact(DisplayName = "Descrição acima de 300 caracteres é rejeitada")]
    public void Criar_DescricaoLonga_Rejeita()
    {
        ValidationResult resultado = _criarValidator.Validate(Base() with { Descricao = new string('a', 301) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarModalidadeCommand.Descricao));
    }

    [Fact(DisplayName = "Atualização com Id vazio é rejeitada")]
    public void Atualizar_IdVazio_Rejeita()
    {
        ValidationResult resultado = _atualizarValidator.Validate(
            new AtualizarModalidadeCommand(Guid.Empty, NaturezaLegal: "AMPLA"));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarModalidadeCommand.Id));
    }

    [Fact(DisplayName = "Atualização válida passa no validator")]
    public void Atualizar_Valido_Passa()
    {
        _atualizarValidator.Validate(
            new AtualizarModalidadeCommand(Guid.CreateVersion7(), NaturezaLegal: "AMPLA", ComposicaoVagas: "RESIDUAL_DO_VO"))
            .IsValid.Should().BeTrue();
    }
}
