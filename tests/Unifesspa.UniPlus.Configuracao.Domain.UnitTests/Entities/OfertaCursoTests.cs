namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDaOfertaDeCurso;

public sealed class OfertaCursoTests
{
    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativa com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        UnidadeOfertante unidade = Unidade();

        OfertaCurso oferta = Criar(unidade: unidade).Value!;

        ConferirIdentidade(oferta, unidade);
        ConferirDeclaracaoPadrao(oferta);
    }

    private static void ConferirIdentidade(OfertaCurso oferta, UnidadeOfertante unidade)
    {
        oferta.Id.Should().NotBe(Guid.Empty);
        oferta.Codigo.Should().Be("ENF_BACHARELADO_MARABA");
        oferta.CursoId.Should().Be(CursoId);
        oferta.LocalOfertaId.Should().Be(LocalOfertaId);
        oferta.UnidadeOfertante.Should().Be(unidade);
        oferta.IsDeleted.Should().BeFalse();
    }

    private static void ConferirDeclaracaoPadrao(OfertaCurso oferta)
    {
        oferta.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Regular);
        oferta.FormatoPedagogico.Should().Be(FormatoPedagogico.Presencial);
        oferta.RegimeDeFuncionamento.Should().Be(RegimeDeFuncionamento.Extensivo);
        oferta.RegimeDeTurno.Should().Be(RegimeDeTurno.Regular);
        oferta.Turnos.Should().Equal(TurnoOferta.Matutino);
        oferta.EMecCodigo.Should().Be("123456");
        oferta.CodigoSga.Should().Be("ENG-01");
        oferta.VagasAnuaisAutorizadas.Should().Be(40);
        oferta.BaseLegal.Should().BeNull();
        oferta.AtoAutorizacaoMec.Should().Be("Portaria MEC 123/2020");
    }

    [Fact(DisplayName = "Criar com unidade ofertante nula lança — o snapshot é pré-requisito do handler")]
    public void Criar_UnidadeNula_Lanca()
    {
        Action act = () => OfertaCurso.Criar(
            CursoId, LocalOfertaId, null!,
            new DeclaracaoDaOfertaDeCurso("OFERTA_VALIDA", "REGULAR", null, "EXTENSIVO", "REGULAR", ["MATUTINO"]));

        act.Should().Throw<ArgumentNullException>();
    }

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
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = token, BaseLegal = "Lei 12.345/2010" });

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
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = token });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido);
    }

    [Theory(DisplayName = "Cada um dos três tokens canônicos de formato é aceito")]
    [InlineData("PRESENCIAL", FormatoPedagogico.Presencial)]
    [InlineData("SEMIPRESENCIAL", FormatoPedagogico.Semipresencial)]
    [InlineData("EAD", FormatoPedagogico.Ead)]
    public void Criar_FormatoCanonico_Aceita(string token, FormatoPedagogico esperado)
    {
        Criar(d => d with { FormatoPedagogico = token }).Value!.FormatoPedagogico.Should().Be(esperado);
    }

    [Theory(DisplayName = "Formato ausente aplica o default conceitual PRESENCIAL")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemFormato_DefaultPresencial(string? token)
    {
        OfertaCurso oferta = Criar(d => d with { FormatoPedagogico = token }).Value!;

        oferta.FormatoPedagogico.Should().Be(
            FormatoPedagogico.Presencial, "o default conceitual espelha o AMPLA de NaturezasLegais");
    }

    [Theory(DisplayName = "Formato fora do domínio fechado é rejeitado")]
    [InlineData("Presencial")]
    [InlineData("HIBRIDO")]
    [InlineData("2")]
    public void Criar_FormatoInvalido_Falha(string token)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { FormatoPedagogico = token });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.FormatoPedagogicoInvalido);
    }

    [Theory(DisplayName = "Criar com programa não-REGULAR sem base legal é rejeitado")]
    [InlineData("FORMA_PARA")]
    [InlineData("PARFOR")]
    [InlineData("PRONERA")]
    [InlineData("PEPETI")]
    [InlineData("CONVENIO_OUTRO")]
    [InlineData("OUTRO")]
    public void Criar_ProgramaNaoRegularSemBaseLegal_Falha(string programa)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = programa, BaseLegal = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
    }

    [Fact(DisplayName = "Base legal em branco não satisfaz o guard condicional (normaliza para nulo)")]
    public void Criar_ProgramaNaoRegularComBaseLegalEmBranco_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = "PARFOR", BaseLegal = "   " });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
    }

    [Fact(DisplayName = "Criar REGULAR sem base legal é aceito — o guard é condicional ao programa")]
    public void Criar_RegularSemBaseLegal_Aceita()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = "REGULAR", BaseLegal = null });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseLegal.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar na transição Regular→Parfor sem base legal é rejeitado sem mutar o agregado")]
    public void Atualizar_TransicaoRegularParforSemBaseLegal_Falha()
    {
        OfertaCurso oferta = Criar(d => d with { ProgramaDeOferta = "REGULAR", BaseLegal = null }).Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "PARFOR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"], "123456", "ENG-01",
            40, null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular);
        oferta.ProgramaDeOferta.Should().Be(
            ProgramaDeOferta.Regular, "a falha de validação não muta o agregado");
    }

    [Fact(DisplayName = "Atualizar na transição Regular→Parfor com base legal é aceito")]
    public void Atualizar_TransicaoRegularParforComBaseLegal_Aceita()
    {
        OfertaCurso oferta = Criar(d => d with { ProgramaDeOferta = "REGULAR", BaseLegal = null }).Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "PARFOR", "SEMIPRESENCIAL", "EXTENSIVO", "REGULAR", ["NOTURNO"], "654321",
            "PED-02", 50, "Decreto 6.755/2009", "Portaria MEC 9/2009"));

        resultado.IsSuccess.Should().BeTrue();
        ConferirOfertaDaTransicaoParaParfor(oferta);
    }

    private static void ConferirOfertaDaTransicaoParaParfor(OfertaCurso oferta)
    {
        oferta.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Parfor);
        oferta.FormatoPedagogico.Should().Be(FormatoPedagogico.Semipresencial);
        oferta.Turnos.Should().Equal(TurnoOferta.Noturno);
        oferta.EMecCodigo.Should().Be("654321");
        oferta.CodigoSga.Should().Be("PED-02");
        oferta.VagasAnuaisAutorizadas.Should().Be(50);
        oferta.BaseLegal.Should().Be("Decreto 6.755/2009");
        oferta.AtoAutorizacaoMec.Should().Be("Portaria MEC 9/2009");
    }

    [Theory(DisplayName = "Código ausente ou fora do formato é recusado em erro de campo codigo")]
    [InlineData(null, OfertaCursoErrorCodes.CodigoObrigatorio)]
    [InlineData("", OfertaCursoErrorCodes.CodigoObrigatorio)]
    [InlineData("   ", OfertaCursoErrorCodes.CodigoObrigatorio)]
    [InlineData("enf_maraba", OfertaCursoErrorCodes.CodigoFormatoInvalido)]
    [InlineData("1ENF", OfertaCursoErrorCodes.CodigoFormatoInvalido)]
    [InlineData("E", OfertaCursoErrorCodes.CodigoFormatoInvalido)]
    [InlineData("ENF-MARABA", OfertaCursoErrorCodes.CodigoFormatoInvalido)]
    [InlineData("ENF MARABA", OfertaCursoErrorCodes.CodigoFormatoInvalido)]
    [InlineData("ENFERMAGEM_BACHARELADO_CAMPUS_MARABA_TURNO_NOTURNO_2027_X", OfertaCursoErrorCodes.CodigoFormatoInvalido)]
    public void Criar_CodigoInvalido_Falha(string? codigo, string codigoDoErro)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { Codigo = codigo });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "codigo" && e.Error.Code == codigoDoErro);
    }

    [Theory(DisplayName = "Código no limite do formato é aceito, com os espaços das pontas descartados")]
    [InlineData("EN", "EN")]
    [InlineData("  ENF_BACH_MARABA  ", "ENF_BACH_MARABA")]
    [InlineData("A1234567890123456789012345678901234567890123456789", "A1234567890123456789012345678901234567890123456789")]
    public void Criar_CodigoNoLimite_Aceita(string informado, string esperado) =>
        Criar(d => d with { Codigo = informado }).Value!.Codigo.Should().Be(esperado);

    [Fact(DisplayName = "Atualizar troca o código e mantém a identidade da oferta")]
    public void Atualizar_TrocaCodigo_MantemIdentidade()
    {
        OfertaCurso oferta = Criar().Value!;
        Guid id = oferta.Id;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "ENF_BACH_MARABA", "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"]));

        resultado.IsSuccess.Should().BeTrue();
        oferta.Codigo.Should().Be("ENF_BACH_MARABA");
        oferta.Id.Should().Be(id);
    }

    [Fact(DisplayName = "Atualizar com código inválido recusa sem mutar o agregado")]
    public void Atualizar_CodigoInvalido_RecusaSemMutar()
    {
        OfertaCurso oferta = Criar().Value!;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "enf", "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"]));

        resultado.IsFailure.Should().BeTrue();
        oferta.Codigo.Should().Be("ENF_BACHARELADO_MARABA");
    }

    [Fact(DisplayName = "Atualizar não altera CursoId, LocalOfertaId, UnidadeOfertante nem Id (imutáveis)")]
    public void Atualizar_PreservaCursoLocalUnidadeEId()
    {
        UnidadeOfertante unidade = Unidade();
        OfertaCurso oferta = Criar(unidade: unidade).Value!;
        Guid idOriginal = oferta.Id;

        Result resultado = oferta.Atualizar(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_EDITADA", "OUTRO", "EAD", "EXTENSIVO", "REGULAR", ["NOTURNO"], null, null, null,
            "Resolução CONSEPE 1/2026", null));

        resultado.IsSuccess.Should().BeTrue();
        oferta.Id.Should().Be(idOriginal);
        oferta.CursoId.Should().Be(CursoId, "mudar o curso caracteriza outra oferta");
        oferta.LocalOfertaId.Should().Be(LocalOfertaId, "mudar o local caracteriza outra oferta");
        oferta.UnidadeOfertante.Should().Be(unidade, "o snapshot congelado (ADR-0061) é imutável pós-criação");
    }
}
