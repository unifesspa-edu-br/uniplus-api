namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDaOfertaDeCurso;

public sealed class OfertaCursoFuncionamentoTests
{
    [Theory(DisplayName = "Os três códigos de regime de funcionamento são aceitos")]
    [InlineData("EXTENSIVO", "REGULAR", new[] { "MATUTINO" }, RegimeDeFuncionamento.Extensivo)]
    [InlineData("EXTENSIVO", "INTEGRAL", new[] { "MATUTINO", "VESPERTINO" }, RegimeDeFuncionamento.Extensivo)]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "REGULAR", new[] { "MATUTINO" }, RegimeDeFuncionamento.AlternanciaPedagogica)]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "INTEGRAL", new[] { "MATUTINO", "VESPERTINO" }, RegimeDeFuncionamento.AlternanciaPedagogica)]
    [InlineData("INTENSIVO", "INTEGRAL", new[] { "MATUTINO", "VESPERTINO" }, RegimeDeFuncionamento.Intensivo)]
    public void Criar_FuncionamentoCanonico_Aceita(
        string funcionamento, string regimeDeTurno, string[] turnos, RegimeDeFuncionamento esperado)
    {
        OfertaCurso oferta = Criar(d => d with { RegimeDeFuncionamento = funcionamento, RegimeDeTurno = regimeDeTurno, Turnos = turnos }).Value!;

        oferta.RegimeDeFuncionamento.Should().Be(esperado);
    }

    [Theory(DisplayName = "Oferta extensiva regular aceita qualquer um dos três turnos")]
    [InlineData("MATUTINO")]
    [InlineData("VESPERTINO")]
    [InlineData("NOTURNO")]
    public void Criar_ExtensivoRegular_AceitaQualquerTurno(string turno)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = "EXTENSIVO", RegimeDeTurno = "REGULAR", Turnos = [turno] });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Regime de funcionamento ausente é recusado por erro próprio, não presumido")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_FuncionamentoAusente_FalhaComObrigatorio(string? codigo)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = codigo });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeFuncionamentoObrigatorio);
        resultado.Errors[0].Field.Should().Be("regimeDeFuncionamento");
    }

    [Fact(DisplayName = "Regime de turno INTEGRAL com dois turnos não presume oferta intensiva")]
    public void Criar_IntegralComDoisTurnosSemFuncionamento_NaoInfere()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = null, RegimeDeTurno = "INTEGRAL", Turnos = ["MATUTINO", "VESPERTINO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeFuncionamentoObrigatorio);
    }

    [Theory(DisplayName = "Código de regime de funcionamento fora do domínio fechado é recusado")]
    [InlineData("SEMI_INTENSIVO")]
    [InlineData("Intensivo")]
    [InlineData("intensivo")]
    [InlineData("1")]
    [InlineData("INTEGRAL")]
    [InlineData("PRESENCIAL")]
    [InlineData(" INTENSIVO")]
    [InlineData("INTENSIVO ")]
    public void Criar_FuncionamentoInvalido_Falha(string codigo)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = codigo });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeFuncionamentoInvalido);
        resultado.Errors[0].Field.Should().Be("regimeDeFuncionamento");
    }

    [Fact(DisplayName = "Oferta intensiva com regime de turno regular é recusada, sem converter nenhuma das dimensões")]
    public void Criar_IntensivoComRegular_RecusaSemConverter()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = "INTENSIVO", RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(
            e => e.Error.Code == OfertaCursoErrorCodes.RegimeDeFuncionamentoIncompativelComRegimeDeTurno
                 && e.Field == "regimeDeFuncionamento");
        resultado.Error!.Message.Should().Contain("INTENSIVO").And.Contain("INTEGRAL");
    }

    [Fact(DisplayName = "Regime de funcionamento inválido não deriva erro de incompatibilidade")]
    public void Criar_FuncionamentoInvalidoComRegular_NaoDerivaIncompatibilidade()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = "SEMI_INTENSIVO", RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeFuncionamentoInvalido);
    }

    [Fact(DisplayName = "Regime de turno inválido não deriva erro de incompatibilidade com o funcionamento")]
    public void Criar_IntensivoComRegimeDeTurnoInvalido_NaoDerivaIncompatibilidade()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = "INTENSIVO", RegimeDeTurno = "PARCIAL", Turnos = ["MATUTINO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoInvalido);
    }

    [Fact(DisplayName = "As falhas das duas dimensões acumulam no mesmo Result, sem retorno antecipado")]
    public void Criar_FuncionamentoEProgramaInvalidos_AcumulaAmbos()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = "PROUNI", RegimeDeFuncionamento = "SEMI_INTENSIVO" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain([
            OfertaCursoErrorCodes.ProgramaDeOfertaInvalido,
            OfertaCursoErrorCodes.RegimeDeFuncionamentoInvalido,
        ]);
    }

    [Fact(DisplayName = "Oferta intensiva com um turno acumula a incompatibilidade e a cardinalidade")]
    public void Criar_IntensivoRegularComUmTurno_AcumulaAmbasAsFalhas()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { RegimeDeFuncionamento = "INTENSIVO", RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO", "NOTURNO"] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain([
            OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime,
            OfertaCursoErrorCodes.RegimeDeFuncionamentoIncompativelComRegimeDeTurno,
        ]);
    }

    [Fact(DisplayName = "Atualizar aplica a mesma invariante de compatibilidade e não muta o agregado ao recusar")]
    public void Atualizar_ParaIntensivoMantendoRegular_RecusaSemMutar()
    {
        OfertaCurso oferta = Criar(d => d with { RegimeDeFuncionamento = "EXTENSIVO", RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO"] }).Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "REGULAR", "PRESENCIAL", "INTENSIVO", "REGULAR", ["MATUTINO"],
            null, null, null, null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should()
            .Be(OfertaCursoErrorCodes.RegimeDeFuncionamentoIncompativelComRegimeDeTurno);
        oferta.RegimeDeFuncionamento.Should().Be(
            RegimeDeFuncionamento.Extensivo, "a falha de validação não converte nem muta o agregado");
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Regular);
        oferta.Turnos.Should().Equal(TurnoOferta.Matutino);
    }

    [Fact(DisplayName = "Atualizar promove a oferta a intensiva quando o regime de turno acompanha")]
    public void Atualizar_ParaIntensivoComIntegral_Aceita()
    {
        OfertaCurso oferta = Criar(d => d with { RegimeDeFuncionamento = "EXTENSIVO", RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO"] }).Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "REGULAR", "PRESENCIAL", "INTENSIVO", "INTEGRAL", ["MATUTINO", "VESPERTINO"],
            null, null, null, null, null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        oferta.RegimeDeFuncionamento.Should().Be(RegimeDeFuncionamento.Intensivo);
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
    }

    [Fact(DisplayName = "Atualizar sem regime de funcionamento é recusado — o campo não é opcional na edição")]
    public void Atualizar_SemFuncionamento_Falha()
    {
        OfertaCurso oferta = Criar().Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "REGULAR", "PRESENCIAL", null, "REGULAR", ["MATUTINO"],
            null, null, null, null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeFuncionamentoObrigatorio);
    }

    [Fact(DisplayName = "Atualizar troca o regime e a coleção de turnos por inteiro")]
    public void Atualizar_TrocaRegimeETurnos()
    {
        OfertaCurso oferta = Criar(d => d with { RegimeDeTurno = "REGULAR", Turnos = ["MATUTINO"] }).Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "REGULAR", "PRESENCIAL", "EXTENSIVO", "INTEGRAL", ["NOTURNO", "VESPERTINO"],
            null, null, null, null, null));

        resultado.IsSuccess.Should().BeTrue();
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
        oferta.Turnos.Should().Equal(TurnoOferta.Vespertino, TurnoOferta.Noturno);
    }
}
