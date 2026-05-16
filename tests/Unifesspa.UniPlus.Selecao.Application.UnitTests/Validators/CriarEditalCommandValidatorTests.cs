namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;
using Unifesspa.UniPlus.Selecao.Application.Validators;

/// <summary>
/// Cobertura do <see cref="CriarEditalCommandValidator"/> após a Story #454
/// ter substituído o enum <c>TipoProcesso</c> pela FK preparatória
/// <c>TipoEditalId</c> (Guid?). A regra antiga (<c>IsInEnum</c>) saiu;
/// a nova rejeita <c>Guid.Empty</c> quando informado, sem invalidar
/// o caso opcional (null).
/// </summary>
public sealed class CriarEditalCommandValidatorTests
{
    private static CriarEditalCommand BaseValid(Guid? tipoEditalId = null) => new(
        NumeroEdital: 42,
        AnoEdital: 2026,
        Titulo: "Edital teste",
        TipoEditalId: tipoEditalId,
        MaximoOpcoesCurso: 1);

    [Fact(DisplayName = "Validator passa quando TipoEditalId e null (opcional na Story #454)")]
    public void Aceita_TipoEditalIdNulo()
    {
        ValidationResult result = new CriarEditalCommandValidator().Validate(BaseValid(tipoEditalId: null));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator passa quando TipoEditalId e Guid v7 valido")]
    public void Aceita_TipoEditalIdValido()
    {
        ValidationResult result = new CriarEditalCommandValidator().Validate(BaseValid(tipoEditalId: Guid.CreateVersion7()));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando TipoEditalId = Guid.Empty (informado mas vazio)")]
    public void Rejeita_TipoEditalIdEmpty()
    {
        ValidationResult result = new CriarEditalCommandValidator().Validate(BaseValid(tipoEditalId: Guid.Empty));

        result.IsValid.Should().BeFalse();

        // PropertyName ancorado ao campo do request (`TipoEditalId`, não
        // `TipoEditalId.Value`) — clientes que mapeiam ProblemDetails para
        // input fields esperam a chave `tipoEditalId` no envelope de erro.
        result.Errors.Should().ContainSingle(e => e.PropertyName == "TipoEditalId");
        result.Errors[0].ErrorMessage.Should().Contain("Guid vazio");
    }
}
