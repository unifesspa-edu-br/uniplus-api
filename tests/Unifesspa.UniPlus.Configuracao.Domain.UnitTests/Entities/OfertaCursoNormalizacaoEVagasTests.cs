namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDaOfertaDeCurso;

public sealed class OfertaCursoNormalizacaoEVagasTests
{
    [Fact(DisplayName = "Vagas anuais negativas são rejeitadas")]
    public void Criar_VagasNegativas_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { VagasAnuaisAutorizadas = -1 });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.VagasAnuaisNegativas);
    }

    [Theory(DisplayName = "Vagas anuais zero e nulas são aceitas — o teto e-MEC é opcional")]
    [InlineData(0)]
    [InlineData(null)]
    public void Criar_VagasZeroOuNulas_Aceita(int? vagas)
    {
        Result<OfertaCurso> resultado = Criar(d => d with { VagasAnuaisAutorizadas = vagas });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.VagasAnuaisAutorizadas.Should().Be(vagas);
    }

    [Fact(DisplayName = "Campos textuais opcionais são normalizados por Trim; em branco viram nulos")]
    public void Criar_CamposOpcionais_Normaliza()
    {
        OfertaCurso oferta = Criar(d => d with { EMecCodigo = "  123456  ", CodigoSga = "   ", AtoAutorizacaoMec = "  Portaria MEC 123/2020  " }).Value!;

        oferta.EMecCodigo.Should().Be("123456");
        oferta.CodigoSga.Should().BeNull();
        oferta.AtoAutorizacaoMec.Should().Be("Portaria MEC 123/2020");
    }

    [Fact(DisplayName = "Código e-MEC acima do tamanho máximo é rejeitado")]
    public void Criar_EMecCodigoLongo_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { EMecCodigo = new string('1', 21) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.EMecCodigoTamanho);
    }

    [Fact(DisplayName = "Código SGA acima do tamanho máximo é rejeitado")]
    public void Criar_CodigoSgaLongo_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { CodigoSga = new string('S', 31) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.CodigoSgaTamanho);
    }

    [Fact(DisplayName = "Base legal acima do tamanho máximo é rejeitada")]
    public void Criar_BaseLegalLonga_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = "PARFOR", BaseLegal = new string('B', 501) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "Ato de autorização MEC acima do tamanho máximo é rejeitado")]
    public void Criar_AtoAutorizacaoMecLongo_Falha()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { AtoAutorizacaoMec = new string('A', 301) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.AtoAutorizacaoMecTamanho);
    }

    [Fact(DisplayName = "Programa inválido, turno inválido e vagas negativas acumulam as três violações")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = "PROUNI", Turnos = ["DIURNO"], VagasAnuaisAutorizadas = -1 });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("programaDeOferta");
        resultado.Errors[1].Field.Should().Be("turnos");
        resultado.Errors[2].Field.Should().Be("vagasAnuaisAutorizadas");
    }

    [Fact(DisplayName = "Programa inválido não deriva a exigência de base legal — só o token inválido é reportado")]
    public void Criar_ProgramaInvalidoComBaseLegalAusente_NaoDerivaGuard()
    {
        Result<OfertaCurso> resultado = Criar(d => d with { ProgramaDeOferta = "PROUNI", BaseLegal = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(1,
            "o guard de base legal depende do programa já reconhecido — reportá-lo sobre um " +
            "token que nem chegou a resolver seria um erro derivado, não uma incoerência independente");
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido);
    }

    [Fact(DisplayName = "ValidarDeclaracao isolada é público e reusável sem construir o agregado")]
    public void ValidarDeclaracao_CamposValidos_Aceita()
    {
        Result resultado = OfertaCurso.ValidarDeclaracao(new DeclaracaoDaOfertaDeCurso(
            "OFERTA_VALIDA", "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"], "123456", "ENG-01",
            40, null, null));

        resultado.IsSuccess.Should().BeTrue();
    }
}
