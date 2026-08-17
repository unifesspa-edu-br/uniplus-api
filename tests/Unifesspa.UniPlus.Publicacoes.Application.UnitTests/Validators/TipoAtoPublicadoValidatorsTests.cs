namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Validators;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

/// <summary>
/// O <see cref="RemoverTipoAtoPublicadoCommandValidator"/> é o único validator
/// remanescente do cadastro de tipos de ato: Criar/Atualizar tiveram os seus
/// removidos (ADR-0125) — a validação de formato passou para o agregado
/// (<c>TipoAtoPublicado.ValidarCampos</c>, coberta em
/// <c>Unifesspa.UniPlus.Publicacoes.Domain.UnitTests.TipoAtoPublicadoTests</c>).
/// Remover não tem agregado a consultar (é uma remoção lógica por Id), então o
/// validator de formato do Id continua fazendo sentido aqui.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class TipoAtoPublicadoValidatorsTests
{
    private readonly RemoverTipoAtoPublicadoCommandValidator _remover = new();

    [Fact(DisplayName = "Remover: identificador vazio é recusado")]
    public void Remover_IdVazio()
    {
        _remover.Validate(new RemoverTipoAtoPublicadoCommand(Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Remover: identificador informado passa")]
    public void Remover_Valido()
    {
        _remover.Validate(new RemoverTipoAtoPublicadoCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
