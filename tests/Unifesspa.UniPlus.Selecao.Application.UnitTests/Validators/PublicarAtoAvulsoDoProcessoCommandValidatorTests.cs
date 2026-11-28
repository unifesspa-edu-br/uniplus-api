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

    [Fact(DisplayName = "O catálogo confere o formato do tipo de ato e recusa tipo sem versão vigente")]
    public void Validar_TipoForaDoFormato_Aceita() =>
        _validator.Validate(Comando() with { TipoAtoCodigo = "aviso" }).IsValid.Should().BeTrue();

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

    [Fact(DisplayName = "A data e o documento principal ausentes não são do validator: o domínio os recusa com o código de campo obrigatório")]
    public void DataEDocumentoAusentes_NaoSaoDoValidator() =>
        _validator.Validate(Comando() with { DataDoAto = null, DocumentoPrincipalId = null }).IsValid.Should().BeTrue();

    [Fact(DisplayName = "A data zero informada é recusada, porque não é ausência")]
    public void DataZeroInformada_Recusa() =>
        _validator.Validate(Comando() with { DataDoAto = default(DateOnly) }).IsValid.Should().BeFalse();

    [Theory(DisplayName = "O tipo e o assinante ausentes ou em branco passam pelo validator: a forma do pedido os recusa como campo obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TipoEAssinanteAusentes_NaoSaoDoValidator(string? texto) =>
        _validator.Validate(Comando() with { TipoAtoCodigo = texto, Assinante = texto }).IsValid.Should().BeTrue();

    [Fact(DisplayName = "O tipo e o assinante só com espaços, mesmo acima do limite, são da forma do pedido e não do comprimento")]
    public void TipoEAssinanteSoComEspacosAcimaDoLimite_NaoSaoDoValidator() =>
        _validator.Validate(Comando() with { TipoAtoCodigo = new string(' ', 201), Assinante = new string(' ', 201) }).IsValid.Should().BeTrue();

    [Fact(DisplayName = "O tipo e o assinante acima do tamanho do registro continuam recusados pelo validator")]
    public void TipoEAssinanteAcimaDoTamanho_SaoRecusados()
    {
        PublicarAtoAvulsoDoProcessoCommand comando = Comando() with { TipoAtoCodigo = new string('a', 61), Assinante = new string('b', 201) };

        _validator.Validate(comando).Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["TipoAtoCodigo", "Assinante"]);
    }
}
