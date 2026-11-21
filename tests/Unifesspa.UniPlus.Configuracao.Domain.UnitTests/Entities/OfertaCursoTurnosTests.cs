namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDaOfertaDeCurso;

public sealed class OfertaCursoTurnosTests
{
    [Theory(DisplayName = "Cada um dos três códigos de turno é aceito sob o regime regular")]
    [InlineData("MATUTINO", TurnoOferta.Matutino)]
    [InlineData("VESPERTINO", TurnoOferta.Vespertino)]
    [InlineData("NOTURNO", TurnoOferta.Noturno)]
    public void Criar_TurnoCanonico_Aceita(string codigo, TurnoOferta esperado)
    {
        Criar(d => d with { RegimeDeTurno = "REGULAR", Turnos = [codigo] }).Value!.Turnos.Should().Equal(esperado);
    }

    [Fact(DisplayName = "Regime integral aceita dois turnos distintos")]
    public void Criar_IntegralComDoisTurnos_Aceita()
    {
        OfertaCurso oferta = Criar(d => d with { RegimeDeTurno = "INTEGRAL", Turnos = ["MATUTINO", "VESPERTINO"] }).Value!;

        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
        oferta.Turnos.Should().Equal(TurnoOferta.Matutino, TurnoOferta.Vespertino);
    }

    [Theory(DisplayName = "Regime de turno ausente é rejeitado por erro próprio, distinto do código inválido")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemRegimeDeTurno_Falha(string? codigo)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = codigo });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoObrigatorio);
        resultado.Errors[0].Field.Should().Be("regimeDeTurno");
    }

    [Theory(DisplayName = "Regime de turno fora do domínio fechado é rejeitado")]
    [InlineData("Regular")]
    [InlineData("PARCIAL")]
    [InlineData("MATUTINO")]
    [InlineData("2")]
    [InlineData(" INTEGRAL")]
    [InlineData("INTEGRAL ")]
    [InlineData("integral")]
    public void Criar_RegimeDeTurnoInvalido_Falha(string codigo)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = codigo });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoInvalido);
    }

    [Fact(DisplayName = "Lista de turnos ausente é rejeitada")]
    public void Criar_TurnosNulos_Falha()
    {
        Result<OfertaCurso> resultado = OfertaCurso.Criar(
            CursoId, LocalOfertaId, Unidade(),
            new DeclaracaoDaOfertaDeCurso("OFERTA_VALIDA", "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnosObrigatorios);
        resultado.Errors[0].Field.Should().Be("turnos");
    }

    [Fact(DisplayName = "Lista de turnos vazia é rejeitada")]
    public void Criar_TurnosVazios_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { Turnos = [] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnosObrigatorios);
    }

    [Theory(DisplayName = "Turno fora do domínio fechado é rejeitado")]
    [InlineData("Matutino")]
    [InlineData("DIURNO")]
    [InlineData("3")]
    [InlineData("INTEGRAL")]
    [InlineData("matutino")]
    [InlineData(" MATUTINO")]
    [InlineData("MATUTINO ")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("  ")]
    public void Criar_TurnoInvalido_Falha(string? codigo)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { Turnos = [codigo] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnoInvalido);
        resultado.Errors[0].Field.Should().Be("turnos");
    }

    [Fact(DisplayName = "A mensagem de turno inválido lista os códigos aceitos sem repetir o valor recebido")]
    public void Criar_TurnoInvalido_MensagemListaOsCodigosSemEcoDoValor()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { Turnos = ["DIURNO"] });

        resultado.Error!.Message.Should().Contain("MATUTINO, VESPERTINO, NOTURNO").And.NotContain("DIURNO");
    }

    [Fact(DisplayName = "Turno repetido é rejeitado — os dois turnos do integral devem ser distintos")]
    public void Criar_TurnoRepetido_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = "INTEGRAL", Turnos = ["MATUTINO", "MATUTINO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnoRepetido);
    }

    [Fact(DisplayName = "Regime regular com dois turnos é recusado, não promovido a integral")]
    public void Criar_RegularComDoisTurnos_RecusaSemPromover()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO", "NOTURNO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime);
        resultado.Error!.Message.Should().Contain("REGULAR").And.Contain("1 turno");
    }

    [Fact(DisplayName = "Regime integral com um único turno é recusado")]
    public void Criar_IntegralComUmTurno_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = "INTEGRAL", Turnos = ["NOTURNO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime);
        resultado.Error!.Message.Should().Contain("INTEGRAL").And.Contain("2 turno");
    }

    [Fact(DisplayName = "Regime integral com três turnos é recusado")]
    public void Criar_IntegralComTresTurnos_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = "INTEGRAL", Turnos = ["MATUTINO", "VESPERTINO", "NOTURNO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime);
    }

    [Fact(DisplayName = "Os turnos são devolvidos em ordem canônica, qualquer que seja a ordem de entrada")]
    public void Criar_OrdemDeEntradaInvertida_NormalizaParaOrdemCanonica()
    {
        OfertaCurso oferta = Criar(d => d with { RegimeDeTurno = "INTEGRAL", Turnos = ["VESPERTINO", "MATUTINO"] }).Value!;

        oferta.Turnos.Should().Equal(TurnoOferta.Matutino, TurnoOferta.Vespertino);
    }

    [Theory(DisplayName = "Nenhum formato pedagógico dispensa o turno — a distância inclusive")]
    [InlineData("EAD")]
    [InlineData("SEMIPRESENCIAL")]
    [InlineData("PRESENCIAL")]
    public void Criar_SemTurnoEmQualquerFormato_Falha(string formato)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { FormatoPedagogico = formato, Turnos = [] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnosObrigatorios);
    }

    [Fact(DisplayName = "Regime inválido não deriva erro de cardinalidade — só o código inválido é reportado")]
    public void Criar_RegimeInvalidoComDoisTurnos_NaoDerivaCardinalidade()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = "PARCIAL", Turnos = ["MATUTINO", "VESPERTINO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoInvalido);
    }

    [Fact(DisplayName = "Turno inválido não deriva erro de cardinalidade — só o código inválido é reportado")]
    public void Criar_TurnoInvalidoSobIntegral_NaoDerivaCardinalidade()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeTurno = "INTEGRAL", Turnos = ["DIURNO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnoInvalido);
    }
}
