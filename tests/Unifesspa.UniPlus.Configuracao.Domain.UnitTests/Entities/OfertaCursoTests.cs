namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class OfertaCursoTests
{
    private static readonly Guid CursoId = Guid.CreateVersion7();
    private static readonly Guid LocalOfertaId = Guid.CreateVersion7();

    private static UnidadeOfertante Unidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

    private static Result<OfertaCurso> Criar(
        string? programaDeOferta = "REGULAR",
        string? formatoPedagogico = "PRESENCIAL",
        string? regimeDeTurno = "REGULAR",
        IReadOnlyList<string?>? turnos = null,
        string? eMecCodigo = "123456",
        string? codigoSga = "ENG-01",
        int? vagasAnuaisAutorizadas = 40,
        string? baseLegal = null,
        string? atoAutorizacaoMec = "Portaria MEC 123/2020",
        UnidadeOfertante? unidade = null) =>
        OfertaCurso.Criar(
            CursoId, LocalOfertaId, unidade ?? Unidade(), programaDeOferta, formatoPedagogico,
            regimeDeTurno, turnos ?? ["MATUTINO"], eMecCodigo, codigoSga,
            vagasAnuaisAutorizadas, baseLegal, atoAutorizacaoMec);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativa com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        UnidadeOfertante unidade = Unidade();

        OfertaCurso oferta = Criar(unidade: unidade).Value!;

        oferta.Id.Should().NotBe(Guid.Empty);
        oferta.CursoId.Should().Be(CursoId);
        oferta.LocalOfertaId.Should().Be(LocalOfertaId);
        oferta.UnidadeOfertante.Should().Be(unidade);
        oferta.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Regular);
        oferta.FormatoPedagogico.Should().Be(FormatoPedagogico.Presencial);
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Regular);
        oferta.Turnos.Should().Equal(TurnoOferta.Matutino);
        oferta.EMecCodigo.Should().Be("123456");
        oferta.CodigoSga.Should().Be("ENG-01");
        oferta.VagasAnuaisAutorizadas.Should().Be(40);
        oferta.BaseLegal.Should().BeNull();
        oferta.AtoAutorizacaoMec.Should().Be("Portaria MEC 123/2020");
        oferta.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar com unidade ofertante nula lança — o snapshot é pré-requisito do handler")]
    public void Criar_UnidadeNula_Lanca()
    {
        Action act = () => OfertaCurso.Criar(
            CursoId, LocalOfertaId, null!, "REGULAR", null, "REGULAR", ["MATUTINO"],
            null, null, null, null, null);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Programa de oferta ────────────────────────────────────────────────

    [Theory(DisplayName = "Cada um dos sete tokens canônicos de programa é aceito (não-REGULAR exige base legal)")]
    [InlineData("REGULAR", ProgramaDeOferta.Regular)]
    [InlineData("FORMA_PARA", ProgramaDeOferta.FormaPara)]
    [InlineData("PARFOR", ProgramaDeOferta.Parfor)]
    [InlineData("PRONERA", ProgramaDeOferta.Pronera)]
    [InlineData("PEPETI", ProgramaDeOferta.Pepeti)]
    [InlineData("CONVENIO_OUTRO", ProgramaDeOferta.ConvenioOutro)]
    [InlineData("OUTRO", ProgramaDeOferta.Outro)]
    public void Criar_ProgramaCanonico_Aceita(string token, ProgramaDeOferta esperado)
    {
        Result<OfertaCurso> resultado = Criar(
            programaDeOferta: token, baseLegal: "Lei 12.345/2010");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ProgramaDeOferta.Should().Be(esperado);
    }

    [Theory(DisplayName = "Programa ausente ou fora do domínio fechado é rejeitado (sem default)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Regular")]
    [InlineData("regular")]
    [InlineData("1")]
    [InlineData("PROUNI")]
    public void Criar_ProgramaInvalido_Falha(string? token)
    {
        Result<OfertaCurso> resultado = Criar(programaDeOferta: token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido);
    }

    // ── Formato pedagógico ────────────────────────────────────────────────

    [Theory(DisplayName = "Cada um dos três tokens canônicos de formato é aceito")]
    [InlineData("PRESENCIAL", FormatoPedagogico.Presencial)]
    [InlineData("SEMIPRESENCIAL", FormatoPedagogico.Semipresencial)]
    [InlineData("EAD", FormatoPedagogico.Ead)]
    public void Criar_FormatoCanonico_Aceita(string token, FormatoPedagogico esperado)
    {
        Criar(formatoPedagogico: token).Value!.FormatoPedagogico.Should().Be(esperado);
    }

    [Theory(DisplayName = "Formato ausente aplica o default conceitual PRESENCIAL")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemFormato_DefaultPresencial(string? token)
    {
        OfertaCurso oferta = Criar(formatoPedagogico: token).Value!;

        oferta.FormatoPedagogico.Should().Be(
            FormatoPedagogico.Presencial, "o default conceitual espelha o AMPLA de NaturezasLegais");
    }

    [Theory(DisplayName = "Formato fora do domínio fechado é rejeitado")]
    [InlineData("Presencial")]
    [InlineData("HIBRIDO")]
    [InlineData("2")]
    public void Criar_FormatoInvalido_Falha(string token)
    {
        Result<OfertaCurso> resultado = Criar(formatoPedagogico: token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.FormatoPedagogicoInvalido);
    }

    // ── Regime de turno e turnos (UNI-REQ-0137) ───────────────────────────

    [Theory(DisplayName = "Cada um dos três tokens canônicos de turno é aceito sob o regime regular")]
    [InlineData("MATUTINO", TurnoOferta.Matutino)]
    [InlineData("VESPERTINO", TurnoOferta.Vespertino)]
    [InlineData("NOTURNO", TurnoOferta.Noturno)]
    public void Criar_TurnoCanonico_Aceita(string token, TurnoOferta esperado)
    {
        Criar(regimeDeTurno: "REGULAR", turnos: [token]).Value!.Turnos.Should().Equal(esperado);
    }

    [Fact(DisplayName = "Regime integral aceita dois turnos distintos")]
    public void Criar_IntegralComDoisTurnos_Aceita()
    {
        OfertaCurso oferta = Criar(regimeDeTurno: "INTEGRAL", turnos: ["MATUTINO", "VESPERTINO"]).Value!;

        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
        oferta.Turnos.Should().Equal(TurnoOferta.Matutino, TurnoOferta.Vespertino);
    }

    [Theory(DisplayName = "Regime de turno ausente é rejeitado por erro próprio, distinto do token inválido")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemRegimeDeTurno_Falha(string? token)
    {
        Result<OfertaCurso> resultado = Criar(regimeDeTurno: token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoObrigatorio);
        resultado.Errors[0].Field.Should().Be("regimeDeTurno");
    }

    [Theory(DisplayName = "Regime de turno fora do domínio fechado é rejeitado")]
    [InlineData("Regular")]
    [InlineData("PARCIAL")]
    [InlineData("MATUTINO")]
    [InlineData("2")]
    public void Criar_RegimeDeTurnoInvalido_Falha(string token)
    {
        Result<OfertaCurso> resultado = Criar(regimeDeTurno: token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoInvalido);
    }

    [Fact(DisplayName = "Lista de turnos ausente é rejeitada")]
    public void Criar_TurnosNulos_Falha()
    {
        Result<OfertaCurso> resultado = OfertaCurso.Criar(
            CursoId, LocalOfertaId, Unidade(), "REGULAR", "PRESENCIAL",
            "REGULAR", null, null, null, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnosObrigatorios);
        resultado.Errors[0].Field.Should().Be("turnos");
    }

    [Fact(DisplayName = "Lista de turnos vazia é rejeitada")]
    public void Criar_TurnosVazios_Falha()
    {
        Result<OfertaCurso> resultado = Criar(turnos: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnosObrigatorios);
    }

    [Theory(DisplayName = "Turno fora do domínio fechado é rejeitado")]
    [InlineData("Matutino")]
    [InlineData("DIURNO")]
    [InlineData("3")]
    [InlineData("INTEGRAL")]
    [InlineData(null)]
    [InlineData("  ")]
    public void Criar_TurnoInvalido_Falha(string? token)
    {
        Result<OfertaCurso> resultado = Criar(turnos: [token]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnoInvalido);
        resultado.Errors[0].Field.Should().Be("turnos");
    }

    [Fact(DisplayName = "Turno repetido é rejeitado — os dois turnos do integral devem ser distintos")]
    public void Criar_TurnoRepetido_Falha()
    {
        Result<OfertaCurso> resultado = Criar(regimeDeTurno: "INTEGRAL", turnos: ["MATUTINO", "MATUTINO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnoRepetido);
    }

    [Fact(DisplayName = "Regime regular com dois turnos é recusado, não promovido a integral")]
    public void Criar_RegularComDoisTurnos_RecusaSemPromover()
    {
        Result<OfertaCurso> resultado = Criar(regimeDeTurno: "REGULAR", turnos: ["MATUTINO", "NOTURNO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime);
        resultado.Error!.Message.Should().Contain("REGULAR").And.Contain("1 turno");
    }

    [Fact(DisplayName = "Regime integral com um único turno é recusado")]
    public void Criar_IntegralComUmTurno_Falha()
    {
        Result<OfertaCurso> resultado = Criar(regimeDeTurno: "INTEGRAL", turnos: ["NOTURNO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime);
        resultado.Error!.Message.Should().Contain("INTEGRAL").And.Contain("2 turno");
    }

    [Fact(DisplayName = "Regime integral com três turnos é recusado")]
    public void Criar_IntegralComTresTurnos_Falha()
    {
        Result<OfertaCurso> resultado = Criar(
            regimeDeTurno: "INTEGRAL", turnos: ["MATUTINO", "VESPERTINO", "NOTURNO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime);
    }

    [Fact(DisplayName = "Os turnos são devolvidos em ordem canônica, qualquer que seja a ordem de entrada")]
    public void Criar_OrdemDeEntradaInvertida_NormalizaParaOrdemCanonica()
    {
        OfertaCurso oferta = Criar(regimeDeTurno: "INTEGRAL", turnos: ["VESPERTINO", "MATUTINO"]).Value!;

        oferta.Turnos.Should().Equal(TurnoOferta.Matutino, TurnoOferta.Vespertino);
    }

    [Theory(DisplayName = "Nenhum formato pedagógico dispensa o turno — a distância inclusive")]
    [InlineData("EAD")]
    [InlineData("SEMIPRESENCIAL")]
    [InlineData("PRESENCIAL")]
    public void Criar_SemTurnoEmQualquerFormato_Falha(string formato)
    {
        Result<OfertaCurso> resultado = Criar(formatoPedagogico: formato, turnos: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnosObrigatorios);
    }

    [Fact(DisplayName = "Regime inválido não deriva erro de cardinalidade — só o token inválido é reportado")]
    public void Criar_RegimeInvalidoComDoisTurnos_NaoDerivaCardinalidade()
    {
        Result<OfertaCurso> resultado = Criar(
            regimeDeTurno: "PARCIAL", turnos: ["MATUTINO", "VESPERTINO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.RegimeDeTurnoInvalido);
    }

    [Fact(DisplayName = "Turno inválido não deriva erro de cardinalidade — só o token inválido é reportado")]
    public void Criar_TurnoInvalidoSobIntegral_NaoDerivaCardinalidade()
    {
        Result<OfertaCurso> resultado = Criar(regimeDeTurno: "INTEGRAL", turnos: ["DIURNO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1);
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.TurnoInvalido);
    }

    [Fact(DisplayName = "Atualizar troca o regime e a coleção de turnos por inteiro")]
    public void Atualizar_TrocaRegimeETurnos()
    {
        OfertaCurso oferta = Criar(regimeDeTurno: "REGULAR", turnos: ["MATUTINO"]).Value!;

        Result resultado = oferta.Atualizar(
            "REGULAR", "PRESENCIAL", "INTEGRAL", ["NOTURNO", "VESPERTINO"],
            null, null, null, null, null);

        resultado.IsSuccess.Should().BeTrue();
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Integral);
        oferta.Turnos.Should().Equal(TurnoOferta.Vespertino, TurnoOferta.Noturno);
    }

    // ── Vagas anuais autorizadas (teto e-MEC) ─────────────────────────────

    [Fact(DisplayName = "Vagas anuais negativas são rejeitadas")]
    public void Criar_VagasNegativas_Falha()
    {
        Result<OfertaCurso> resultado = Criar(vagasAnuaisAutorizadas: -1);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.VagasAnuaisNegativas);
    }

    [Theory(DisplayName = "Vagas anuais zero e nulas são aceitas — o teto e-MEC é opcional")]
    [InlineData(0)]
    [InlineData(null)]
    public void Criar_VagasZeroOuNulas_Aceita(int? vagas)
    {
        Result<OfertaCurso> resultado = Criar(vagasAnuaisAutorizadas: vagas);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.VagasAnuaisAutorizadas.Should().Be(vagas);
    }

    // ── Base legal condicional (ADR-0066) ─────────────────────────────────

    [Theory(DisplayName = "Criar com programa não-REGULAR sem base legal é rejeitado")]
    [InlineData("FORMA_PARA")]
    [InlineData("PARFOR")]
    [InlineData("PRONERA")]
    [InlineData("PEPETI")]
    [InlineData("CONVENIO_OUTRO")]
    [InlineData("OUTRO")]
    public void Criar_ProgramaNaoRegularSemBaseLegal_Falha(string programa)
    {
        Result<OfertaCurso> resultado = Criar(programaDeOferta: programa, baseLegal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
    }

    [Fact(DisplayName = "Base legal em branco não satisfaz o guard condicional (normaliza para nulo)")]
    public void Criar_ProgramaNaoRegularComBaseLegalEmBranco_Falha()
    {
        Result<OfertaCurso> resultado = Criar(programaDeOferta: "PARFOR", baseLegal: "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
    }

    [Fact(DisplayName = "Criar REGULAR sem base legal é aceito — o guard é condicional ao programa")]
    public void Criar_RegularSemBaseLegal_Aceita()
    {
        Result<OfertaCurso> resultado = Criar(programaDeOferta: "REGULAR", baseLegal: null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseLegal.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar na transição Regular→Parfor sem base legal é rejeitado sem mutar o agregado")]
    public void Atualizar_TransicaoRegularParforSemBaseLegal_Falha()
    {
        OfertaCurso oferta = Criar(programaDeOferta: "REGULAR", baseLegal: null).Value!;

        Result resultado = oferta.Atualizar(
            "PARFOR", "PRESENCIAL", "REGULAR", ["MATUTINO"], "123456", "ENG-01", 40, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
        oferta.ProgramaDeOferta.Should().Be(
            ProgramaDeOferta.Regular, "a falha de validação não muta o agregado");
    }

    [Fact(DisplayName = "Atualizar na transição Regular→Parfor com base legal é aceito")]
    public void Atualizar_TransicaoRegularParforComBaseLegal_Aceita()
    {
        OfertaCurso oferta = Criar(programaDeOferta: "REGULAR", baseLegal: null).Value!;

        Result resultado = oferta.Atualizar(
            "PARFOR", "SEMIPRESENCIAL", "REGULAR", ["NOTURNO"], "654321", "PED-02", 50,
            "Decreto 6.755/2009", "Portaria MEC 9/2009");

        resultado.IsSuccess.Should().BeTrue();
        oferta.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Parfor);
        oferta.FormatoPedagogico.Should().Be(FormatoPedagogico.Semipresencial);
        oferta.Turnos.Should().Equal(TurnoOferta.Noturno);
        oferta.EMecCodigo.Should().Be("654321");
        oferta.CodigoSga.Should().Be("PED-02");
        oferta.VagasAnuaisAutorizadas.Should().Be(50);
        oferta.BaseLegal.Should().Be("Decreto 6.755/2009");
        oferta.AtoAutorizacaoMec.Should().Be("Portaria MEC 9/2009");
    }

    // ── Imutabilidade curso × local × unidade ─────────────────────────────

    [Fact(DisplayName = "Atualizar não altera CursoId, LocalOfertaId, UnidadeOfertante nem Id (imutáveis)")]
    public void Atualizar_PreservaCursoLocalUnidadeEId()
    {
        UnidadeOfertante unidade = Unidade();
        OfertaCurso oferta = Criar(unidade: unidade).Value!;
        Guid idOriginal = oferta.Id;

        Result resultado = oferta.Atualizar(
            "OUTRO", "EAD", "REGULAR", ["NOTURNO"], null, null, null, "Resolução CONSEPE 1/2026", null);

        resultado.IsSuccess.Should().BeTrue();
        oferta.Id.Should().Be(idOriginal);
        oferta.CursoId.Should().Be(CursoId, "mudar o curso caracteriza outra oferta");
        oferta.LocalOfertaId.Should().Be(LocalOfertaId, "mudar o local caracteriza outra oferta");
        oferta.UnidadeOfertante.Should().Be(unidade, "o snapshot congelado (ADR-0061) é imutável pós-criação");
    }

    // ── Normalização e tamanhos ───────────────────────────────────────────

    [Fact(DisplayName = "Campos textuais opcionais são normalizados por Trim; em branco viram nulos")]
    public void Criar_CamposOpcionais_Normaliza()
    {
        OfertaCurso oferta = Criar(
            eMecCodigo: "  123456  ",
            codigoSga: "   ",
            atoAutorizacaoMec: "  Portaria MEC 123/2020  ").Value!;

        oferta.EMecCodigo.Should().Be("123456");
        oferta.CodigoSga.Should().BeNull();
        oferta.AtoAutorizacaoMec.Should().Be("Portaria MEC 123/2020");
    }

    [Fact(DisplayName = "Código e-MEC acima do tamanho máximo é rejeitado")]
    public void Criar_EMecCodigoLongo_Falha()
    {
        Result<OfertaCurso> resultado = Criar(eMecCodigo: new string('1', 21));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.EMecCodigoTamanho);
    }

    [Fact(DisplayName = "Código SGA acima do tamanho máximo é rejeitado")]
    public void Criar_CodigoSgaLongo_Falha()
    {
        Result<OfertaCurso> resultado = Criar(codigoSga: new string('S', 31));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CodigoSgaTamanho);
    }

    [Fact(DisplayName = "Base legal acima do tamanho máximo é rejeitada")]
    public void Criar_BaseLegalLonga_Falha()
    {
        Result<OfertaCurso> resultado = Criar(
            programaDeOferta: "PARFOR", baseLegal: new string('B', 501));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "Ato de autorização MEC acima do tamanho máximo é rejeitado")]
    public void Criar_AtoAutorizacaoMecLongo_Falha()
    {
        Result<OfertaCurso> resultado = Criar(atoAutorizacaoMec: new string('A', 301));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.AtoAutorizacaoMecTamanho);
    }

    // ── Acumulação e gate entre campos independentes (ADR-0125) ────────────────

    [Fact(DisplayName = "Programa inválido, turno inválido e vagas negativas acumulam as três violações")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<OfertaCurso> resultado = Criar(
            programaDeOferta: "PROUNI", turnos: ["DIURNO"], vagasAnuaisAutorizadas: -1);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("programaDeOferta");
        resultado.Errors[1].Field.Should().Be("turnos");
        resultado.Errors[2].Field.Should().Be("vagasAnuaisAutorizadas");
    }

    [Fact(DisplayName = "Programa inválido não deriva a exigência de base legal — só o token inválido é reportado")]
    public void Criar_ProgramaInvalidoComBaseLegalAusente_NaoDerivaGuard()
    {
        Result<OfertaCurso> resultado = Criar(programaDeOferta: "PROUNI", baseLegal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1,
            "o guard de base legal depende do programa já reconhecido — reportá-lo sobre um " +
            "token que nem chegou a resolver seria um erro derivado, não uma incoerência independente");
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido);
    }

    [Fact(DisplayName = "ValidarCamposDoPayload isolado é público e reusável sem construir o agregado")]
    public void ValidarCamposDoPayload_CamposValidos_Aceita()
    {
        Result resultado = OfertaCurso.ValidarCamposDoPayload(
            "REGULAR", "PRESENCIAL", "REGULAR", ["MATUTINO"], "123456", "ENG-01", 40, null, null);

        resultado.IsSuccess.Should().BeTrue();
    }
}
