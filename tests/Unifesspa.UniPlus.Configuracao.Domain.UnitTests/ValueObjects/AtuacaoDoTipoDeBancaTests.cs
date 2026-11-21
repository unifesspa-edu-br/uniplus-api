namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class AtuacaoDoTipoDeBancaTests
{
    private const string PapelQueAvalia = "AVALIA_O_MERITO";

    private static Result<AtuacaoDoTipoDeBanca> Validar(string? papel, params string?[]? tipos) =>
        AtuacaoDoTipoDeBanca.Validar(new DeclaracaoDoTipoDeBanca("Banca", Papel: papel, TiposDeDecisaoJulgados: tipos));

    [Fact(DisplayName = "O papel que avalia o mérito com tipos de decisão é aceito, e os tipos saem em ordem alfabética do código")]
    public void Validar_AvaliaOMeritoComTipos_Aceita()
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(PapelQueAvalia, "NOTA_DA_ETAPA", "CLASSIFICACAO", "ANALISE_DOCUMENTAL");

        r.IsSuccess.Should().BeTrue();
        r.Value!.TiposDeDecisaoJulgados.Should().Equal(
            TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.NotaDaEtapa);
    }

    [Theory(DisplayName = "Cada papel que não avalia o mérito é aceito sem tipos de decisão")]
    [InlineData("JULGA_A_PRIMEIRA_INSTANCIA", PapelDaBanca.JulgaAPrimeiraInstancia)]
    [InlineData("IMPORTA_AS_NOTAS_DO_INEP", PapelDaBanca.ImportaAsNotasDoInep)]
    [InlineData("IMPORTA_CANDIDATOS", PapelDaBanca.ImportaCandidatos)]
    [InlineData("JULGA_A_SEGUNDA_INSTANCIA", PapelDaBanca.JulgaASegundaInstancia)]
    public void Validar_OutroPapelSemTipos_Aceita(string codigo, PapelDaBanca esperado)
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(codigo);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Papel.Should().Be(esperado);
        r.Value.TiposDeDecisaoJulgados.Should().BeEmpty();
    }

    [Theory(DisplayName = "Papel ausente ou em branco é recusado como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_PapelAusente_PapelObrigatorio(string? papel)
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(papel);

        r.Errors.Should().ContainSingle();
        r.Errors[0].Field.Should().Be("papel");
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.PapelObrigatorio);
        r.Error.Message.Should().Be("Informe o papel do tipo de banca.");
    }

    [Theory(DisplayName = "Papel escrito de outro jeito ou desconhecido é recusado, com os rótulos aceitos e sem repetir o valor")]
    [InlineData("avalia_o_merito")]
    [InlineData(" AVALIA_O_MERITO")]
    [InlineData("PAPEL_QUE_NAO_EXISTE")]
    public void Validar_PapelForaDoVocabulario_ListaOsAceitosSemRepetirOValor(string papel)
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(papel);

        r.Error!.Code.Should().Be(TipoBancaErrorCodes.PapelForaDoVocabulario);
        r.Error.Message.Should().Be(
            "O papel informado não é um dos aceitos. Papéis aceitos: Avalia o mérito; "
            + "Julga o recurso em 1ª instância; Importa as notas do INEP; Importa candidatos; "
            + "Julga o recurso em 2ª instância.");
    }

    [Fact(DisplayName = "Tipo de decisão nulo, em branco ou desconhecido é recusado por posição, sem repetir o valor, e o repetido a partir da segunda ocorrência")]
    public void Validar_TiposInvalidosERepetidos_RecusaPorPosicao()
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(PapelQueAvalia, "NOTA_DA_ETAPA", null, "  ", "TIPO_QUE_NAO_EXISTE", "NOTA_DA_ETAPA", "CLASSIFICACAO");

        r.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("tiposDeDecisaoJulgados[1]", TipoBancaErrorCodes.TipoDeDecisaoForaDoVocabulario),
            ("tiposDeDecisaoJulgados[2]", TipoBancaErrorCodes.TipoDeDecisaoForaDoVocabulario),
            ("tiposDeDecisaoJulgados[3]", TipoBancaErrorCodes.TipoDeDecisaoForaDoVocabulario),
            ("tiposDeDecisaoJulgados[4]", TipoBancaErrorCodes.TipoDeDecisaoRepetido));
        r.Errors[3].Error.Message.Should().Be("O tipo de decisão 'Nota da etapa' aparece mais de uma vez entre os que o tipo de banca julga.");
        r.Errors[0].Error.Message.Should().Be(
            "Um dos tipos de decisão informados não é um dos aceitos. Tipos de decisão aceitos: Isenção da taxa; Homologação das inscrições; "
            + "Análise documental; Heteroidentificação; Avaliação biopsicossocial; Classificação; Gabarito da prova; Nota da etapa.");
        r.Errors.Should().OnlyContain(static e => !e.Error.Message.Contains("TIPO_QUE_NAO_EXISTE", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "O papel que avalia o mérito sem tipo de decisão, ou lista ausente, é incoerente")]
    [InlineData(PapelQueAvalia, null)]
    [InlineData(PapelQueAvalia, new string[0])]
    public void Validar_AvaliaOMeritoSemTipos_Incoerente(string papel, string[]? tipos)
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(papel, tipos);

        r.Errors.Should().ContainSingle();
        r.Errors[0].Field.Should().Be("tiposDeDecisaoJulgados");
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.TiposDeDecisaoIncoerentesComOPapel);
    }

    [Theory(DisplayName = "Os papéis que não avaliam o mérito com tipo de decisão são incoerentes")]
    [InlineData("JULGA_A_PRIMEIRA_INSTANCIA")]
    [InlineData("IMPORTA_AS_NOTAS_DO_INEP")]
    [InlineData("IMPORTA_CANDIDATOS")]
    [InlineData("JULGA_A_SEGUNDA_INSTANCIA")]
    public void Validar_OutroPapelComTipos_Incoerente(string papel)
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar(papel, "CLASSIFICACAO");

        r.Errors.Should().ContainSingle();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.TiposDeDecisaoIncoerentesComOPapel);
        r.Error.Message.Should().Be(
            "O tipo de banca que avalia o mérito declara ao menos um tipo de decisão que julga; os tipos com outro papel não declaram nenhum.");
    }

    [Fact(DisplayName = "Com o papel recusado, a coerência não é julgada, e o erro do papel acumula com o do tipo de decisão")]
    public void Validar_PapelInvalidoETipoInvalido_AcumulaSemJulgarACoerencia()
    {
        Result<AtuacaoDoTipoDeBanca> r = Validar("PAPEL_QUE_NAO_EXISTE", "TIPO_QUE_NAO_EXISTE");

        r.Errors.Select(static e => e.Error.Code).Should().Equal(
            TipoBancaErrorCodes.PapelForaDoVocabulario, TipoBancaErrorCodes.TipoDeDecisaoForaDoVocabulario);
    }

    [Fact(DisplayName = "As duas marcas chegam como declaradas")]
    public void Validar_Marcas_ChegamComoDeclaradas()
    {
        Result<AtuacaoDoTipoDeBanca> r = AtuacaoDoTipoDeBanca.Validar(new DeclaracaoDoTipoDeBanca(
            "Banca", Papel: "IMPORTA_CANDIDATOS", ExigeDuasOuMaisBancas: true, EncerraRecursoNaPrimeiraInstancia: true));

        r.Value!.ExigeDuasOuMaisBancas.Should().BeTrue();
        r.Value.EncerraRecursoNaPrimeiraInstancia.Should().BeTrue();
    }

    [Fact(DisplayName = "Duas atuações com o mesmo papel, marcas e tipos de decisão são iguais, e com tipos diferentes não são")]
    public void Igualdade_ComparaOsTiposDeDecisaoUmAUm()
    {
        AtuacaoDoTipoDeBanca primeira = Validar(PapelQueAvalia, "NOTA_DA_ETAPA", "CLASSIFICACAO").Value!;
        AtuacaoDoTipoDeBanca igual = Validar(PapelQueAvalia, "CLASSIFICACAO", "NOTA_DA_ETAPA").Value!;
        AtuacaoDoTipoDeBanca diferente = Validar(PapelQueAvalia, "NOTA_DA_ETAPA").Value!;

        primeira.Should().Be(igual);
        primeira.GetHashCode().Should().Be(igual.GetHashCode());
        primeira.Should().NotBe(diferente);
    }
}
