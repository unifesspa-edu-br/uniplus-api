namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Validators;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

/// <summary>
/// Os validators remanescentes do cadastro de tipos de ato checam só forma de
/// identificador de rota, sem equivalente no agregado (ADR-0125) — Criar teve o
/// seu removido por inteiro (não recebe Id), e Atualizar/Remover mantêm cada um
/// uma checagem mínima de <c>Id</c>. A validação de formato dos demais campos
/// passou para o agregado (<c>TipoAtoPublicado.ValidarCampos</c>, coberta em
/// <c>Unifesspa.UniPlus.Publicacoes.Domain.UnitTests.TipoAtoPublicadoTests</c>).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class TipoAtoPublicadoValidatorsTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly AtualizarTipoAtoPublicadoCommandValidator _atualizar = new();
    private readonly RemoverTipoAtoPublicadoCommandValidator _remover = new();

    [Fact(DisplayName = "Atualizar: identificador vazio é recusado")]
    public void Atualizar_IdVazio()
    {
        AtualizarTipoAtoPublicadoCommand comando = new(
            Guid.Empty, "EDITAL_ABERTURA", "Edital de abertura", true, true, false, Inicio);

        _atualizar.Validate(comando).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Atualizar: identificador informado passa")]
    public void Atualizar_IdInformado()
    {
        AtualizarTipoAtoPublicadoCommand comando = new(
            Guid.NewGuid(), "EDITAL_ABERTURA", "Edital de abertura", true, true, false, Inicio);

        _atualizar.Validate(comando).IsValid.Should().BeTrue();
    }

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
