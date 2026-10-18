namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Validators.AtosDoProcesso;

public sealed class PublicarAtoAvulsoDoProcessoCommandValidatorTests
{
    private readonly PublicarAtoAvulsoDoProcessoCommandValidator _validator = new();

    private static PublicarAtoAvulsoDoProcessoCommand Comando(Guid? atoCorrigidoId = null, string? motivo = null) => new(
        Guid.CreateVersion7(), "AVISO", "5/2026", new DateOnly(2026, 12, 10), "Diretor do CEPS", Guid.CreateVersion7(),
        Anexos: null, AtoCorrigidoId: atoCorrigidoId, MotivoDaCorrecao: motivo);

    [Fact(DisplayName = "O aviso sem correção é válido")]
    public void AvisoSemCorrecao_EValido() => _validator.Validate(Comando()).IsValid.Should().BeTrue();

    [Fact(DisplayName = "A correção com ato corrigido e motivo é válida")]
    public void CorrecaoCompleta_EValida() =>
        _validator.Validate(Comando(Guid.CreateVersion7(), "Data errada")).IsValid.Should().BeTrue();

    [Fact(DisplayName = "O par ato corrigido e motivo é regra do agregado: o validator deixa passar o par incoerente")]
    public void ParDaCorrecaoIncoerente_NaoEDoValidator()
    {
        _validator.Validate(Comando(Guid.CreateVersion7(), motivo: null)).IsValid.Should().BeTrue();
        _validator.Validate(Comando(motivo: "Data errada")).IsValid.Should().BeTrue();
        _validator.Validate(Comando(Guid.Empty, "Data errada")).IsValid.Should().BeTrue();
    }
}
