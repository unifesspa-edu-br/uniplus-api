namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

/// <summary>
/// O validator antecipa a obrigatoriedade das referências (curso, local,
/// unidade ofertante) e do programa, os domínios fechados dos tokens (quando há
/// valor efetivo), o teto de vagas não-negativo e os comprimentos máximos —
/// fronteira simétrica com o domínio (#749). O guard condicional da base legal
/// (programa ≠ REGULAR) é regra de domínio (422), não do validator.
/// </summary>
public sealed class OfertaCursoValidatorTests
{
    private readonly CriarOfertaCursoCommandValidator _validator = new();
    private readonly AtualizarOfertaCursoCommandValidator _atualizarValidator = new();

    private static CriarOfertaCursoCommand Base() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "REGULAR", "PRESENCIAL", "MATUTINO", "123456", "ENG-01", 40, null, null);

    [Fact(DisplayName = "Comando válido passa no validator")]
    public void Valido_Passa()
    {
        _validator.Validate(Base()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando mínimo (só referências + programa) passa no validator")]
    public void Minimo_Passa()
    {
        var comando = new CriarOfertaCursoCommand(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "REGULAR");

        _validator.Validate(comando).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "CursoId vazio é rejeitado")]
    public void CursoIdVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { CursoId = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarOfertaCursoCommand.CursoId));
    }

    [Fact(DisplayName = "LocalOfertaId vazio é rejeitado")]
    public void LocalOfertaIdVazio_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(Base() with { LocalOfertaId = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarOfertaCursoCommand.LocalOfertaId));
    }

    [Fact(DisplayName = "UnidadeOfertanteOrigemId vazio é rejeitado")]
    public void UnidadeOrigemVazia_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { UnidadeOfertanteOrigemId = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(CriarOfertaCursoCommand.UnidadeOfertanteOrigemId));
    }

    [Theory(DisplayName = "Programa ausente ou fora do domínio fechado é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Regular")]
    [InlineData("PROUNI")]
    [InlineData("1")]
    public void ProgramaInvalido_Rejeita(string programa)
    {
        ValidationResult resultado = _validator.Validate(Base() with { ProgramaDeOferta = programa });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(CriarOfertaCursoCommand.ProgramaDeOferta));
    }

    [Theory(DisplayName = "Cada um dos sete tokens canônicos de programa é aceito")]
    [InlineData("REGULAR")]
    [InlineData("FORMA_PARA")]
    [InlineData("PARFOR")]
    [InlineData("PRONERA")]
    [InlineData("PEPETI")]
    [InlineData("CONVENIO_OUTRO")]
    [InlineData("OUTRO")]
    public void ProgramaCanonico_Aceita(string programa)
    {
        // O guard da base legal para programas não-REGULAR é de domínio, não do validator.
        _validator.Validate(Base() with { ProgramaDeOferta = programa })
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Formato pedagógico em branco passa (default PRESENCIAL é do domínio)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemFormato_Passa(string? formato)
    {
        _validator.Validate(Base() with { FormatoPedagogico = formato })
            .IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Formato pedagógico fora do domínio fechado é rejeitado")]
    [InlineData("Presencial")]
    [InlineData("HIBRIDO")]
    public void FormatoInvalido_Rejeita(string formato)
    {
        ValidationResult resultado = _validator.Validate(Base() with { FormatoPedagogico = formato });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(CriarOfertaCursoCommand.FormatoPedagogico));
    }

    [Theory(DisplayName = "Turno em branco passa (nulo aceito — nem toda oferta declara turno)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemTurno_Passa(string? turno)
    {
        _validator.Validate(Base() with { Turno = turno }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Turno fora do domínio fechado é rejeitado")]
    [InlineData("Matutino")]
    [InlineData("DIURNO")]
    public void TurnoInvalido_Rejeita(string turno)
    {
        ValidationResult resultado = _validator.Validate(Base() with { Turno = turno });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarOfertaCursoCommand.Turno));
    }

    [Fact(DisplayName = "Vagas anuais negativas são rejeitadas")]
    public void VagasNegativas_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { VagasAnuaisAutorizadas = -1 });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(CriarOfertaCursoCommand.VagasAnuaisAutorizadas));
    }

    [Theory(DisplayName = "Vagas anuais zero e nulas passam — o teto e-MEC é opcional")]
    [InlineData(0)]
    [InlineData(null)]
    public void VagasZeroOuNulas_Passa(int? vagas)
    {
        _validator.Validate(Base() with { VagasAnuaisAutorizadas = vagas })
            .IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Código e-MEC acima de 20 caracteres é rejeitado")]
    public void EMecCodigoLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { EMecCodigo = new string('1', 21) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarOfertaCursoCommand.EMecCodigo));
    }

    [Fact(DisplayName = "Código SGA acima de 30 caracteres é rejeitado")]
    public void CodigoSgaLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { CodigoSga = new string('S', 31) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarOfertaCursoCommand.CodigoSga));
    }

    [Fact(DisplayName = "Base legal acima de 500 caracteres é rejeitada")]
    public void BaseLegalLonga_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { BaseLegal = new string('B', 501) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarOfertaCursoCommand.BaseLegal));
    }

    [Fact(DisplayName = "Ato de autorização MEC acima de 300 caracteres é rejeitado")]
    public void AtoAutorizacaoMecLongo_Rejeita()
    {
        ValidationResult resultado = _validator.Validate(
            Base() with { AtoAutorizacaoMec = new string('A', 301) });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(CriarOfertaCursoCommand.AtoAutorizacaoMec));
    }

    // ── Atualizar — espelho do Criar sem as referências imutáveis ─────────

    [Fact(DisplayName = "Atualizar: comando válido passa no validator")]
    public void Atualizar_Valido_Passa()
    {
        var comando = new AtualizarOfertaCursoCommand(
            Guid.CreateVersion7(), "REGULAR", "EAD", null, "654321", "ENG-02", 60, null, null);

        _atualizarValidator.Validate(comando).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar: Id vazio é rejeitado")]
    public void Atualizar_IdVazio_Rejeita()
    {
        var comando = new AtualizarOfertaCursoCommand(Guid.Empty, "REGULAR");

        ValidationResult resultado = _atualizarValidator.Validate(comando);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarOfertaCursoCommand.Id));
    }

    [Fact(DisplayName = "Atualizar: programa fora do domínio fechado é rejeitado")]
    public void Atualizar_ProgramaInvalido_Rejeita()
    {
        var comando = new AtualizarOfertaCursoCommand(Guid.CreateVersion7(), "PROUNI");

        ValidationResult resultado = _atualizarValidator.Validate(comando);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(
            e => e.PropertyName == nameof(AtualizarOfertaCursoCommand.ProgramaDeOferta));
    }
}
