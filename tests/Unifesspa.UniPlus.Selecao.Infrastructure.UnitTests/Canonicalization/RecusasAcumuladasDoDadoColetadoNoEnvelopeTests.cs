namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class RecusasAcumuladasDoDadoColetadoNoEnvelopeTests
{
    private const string Item = "dadosDosFormularios[0]";
    private const string CondicaoIncoerente = "CondicaoDnf.FormaIncoerenteComOperador";
    private const string Malformado = ErrosCodecEnvelope.EnvelopeMalformado;
    private static readonly string[] ValoresInjetados = ["VALOR_FICTICIO_X", "TIPO_INEXISTENTE", "HIPOTESE_FORA", "OUTRO_FORMULARIO", "COMPONENTE_FORA"];

    [Fact]
    public void LerFatosColetados_ExibicaoObrigatoriedadeSegundaRestricaoEImpedimentoInvalidos_PropagaTodasNaOrdem()
    {
        JsonObject campo = ComExibicaoInvalida();
        campo["obrigatoriedade"] = new JsonObject { ["tipo"] = "QUANDO", ["quando"] = CondicoesNoEnvelope.Invalidas(false, true) };
        campo["restricoes"] = new JsonArray(Tamanho(1, 5), OpcoesComCondicaoInvalida());
        campo["impedimento"] = ImpedimentoComCondicaoInvalida();
        Conferir(
            Padrao,
            campo,
            (Item + ".exibicao[0][0].valor", CondicaoIncoerente),
            (Item + ".obrigatoriedade.quando[0][0].valor", CondicaoIncoerente),
            (Item + ".restricoes[1].entradas[0].quando[0][0].valor", CondicaoIncoerente),
            (Item + ".impedimento.quando[0][0].valor", CondicaoIncoerente));
    }

    [Fact]
    public void LerFatosColetados_ExibicaoInvalidaETamanhoComMinimoMaiorQueOMaximo_PropagaAmbas()
    {
        JsonObject campo = ComExibicaoInvalida();
        campo["restricoes"] = new JsonArray(Tamanho(5, 2));
        Conferir(Padrao, campo, (Item + ".exibicao[0][0].valor", CondicaoIncoerente), (Item + ".restricoes[0]", "RestricaoValor.LimitesIncoerentes"));
    }

    [Fact]
    public void LerFatosColetados_ExibicaoEImpedimentoInvalidos_PropagaAmbasSemAcusarOTipoDoCampo()
    {
        JsonObject campo = ComExibicaoInvalida();
        campo["impedimento"] = ImpedimentoComCondicaoInvalida();
        Conferir(Padrao, campo, (Item + ".exibicao[0][0].valor", CondicaoIncoerente), (Item + ".impedimento.quando[0][0].valor", CondicaoIncoerente));
    }

    [Fact]
    public void LerFatosColetados_ExibicaoInvalidaEImpedimentoForaDaInscricao_PropagaAmbas()
    {
        JsonObject campo = ComExibicaoInvalida();
        campo["tipoDeCampo"] = "SIM_OU_NAO";
        campo["formato"] = null;
        campo["formularioCodigo"] = FormulariosDoDocumentoDeTeste.CodigoDaHabilitacao;
        campo["impedimento"] = new JsonObject { ["mensagem"] = "Mensagem", ["quando"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create(true)))) };
        Conferir(FormulariosDoDocumentoDeTeste.ComFormularioForaDaInscricao(), campo, (Item + ".exibicao[0][0].valor", CondicaoIncoerente), (Item + ".impedimento", Malformado));
    }

    [Fact]
    public void LerFatosColetados_MunicipioComMunicipiosDaUfRecusada_NaoAcusaMunicipioSemUf()
    {
        JsonObject campo = Campo();
        campo["tipoDeCampo"] = "MUNICIPIO";
        campo["formato"] = null;
        campo["restricoes"] = new JsonArray(new JsonObject { ["tipo"] = "MUNICIPIOS_DA_UF", ["codigosDosDados"] = new JsonArray() });
        Conferir(Padrao, campo, (Item + ".restricoes[0]", "RestricaoValor.FatosVazios"));
    }

    [Fact]
    public void LerFatosColetados_MunicipioSemRestricaoDeUf_AcusaMunicipioSemUf()
    {
        JsonObject campo = Campo();
        campo["tipoDeCampo"] = "MUNICIPIO";
        campo["formato"] = null;
        Conferir(Padrao, campo, (Item, ItemFormularioErrorCodes.MunicipioSemUf));
    }

    [Fact]
    public void LerFatosColetados_HipoteseLegalETipoDeCampoForaDoVocabulario_PropagaAmbas()
    {
        JsonObject campo = Campo();
        campo["hipoteseLegal"] = "HIPOTESE_FORA";
        campo["tipoDeCampo"] = "TIPO_INEXISTENTE";
        Conferir(Padrao, campo, (Item + ".hipoteseLegal", Malformado), (Item + ".tipoDeCampo", Malformado));
    }

    [Fact]
    public void LerFatosColetados_ComponenteForaDaFormaCanonica_RecusaUmaVezSo()
    {
        JsonObject campo = Campo();
        campo["componenteExibicao"] = " COMPONENTE_FORA";
        Conferir(Padrao, campo, (Item + ".componenteExibicao", Malformado));
    }

    [Fact]
    public void LerFatosColetados_ExibicaoInvalidaEValoresSelecionaveisInvalidos_RecusaDeFormatoPorUltimo()
    {
        JsonObject campo = ComExibicaoInvalida();
        campo["tipoDeCampo"] = "ESCOLHA_UNICA";
        campo["formato"] = null;
        IReadOnlyList<FieldError> causas = Conferir(Padrao, campo, (Item + ".exibicao[0][0].valor", CondicaoIncoerente), (null, Malformado));
        causas[1].Error.Message.Should().Contain($"{Item}.valoresSelecionaveis");
    }

    [Fact]
    public void LerFatosColetados_SoExibicaoInvalida_NaoGeraRecusaExtra()
    {
        Conferir(Padrao, ComExibicaoInvalida(), (Item + ".exibicao[0][0].valor", CondicaoIncoerente));
    }

    [Fact]
    public void LerFatosColetados_ExibicaoInvalidaETipoDaObrigatoriedadeForaDoFormato_EncerraOItemComAExibicaoAntes()
    {
        JsonObject campo = ComExibicaoInvalida();
        campo["obrigatoriedade"] = new JsonObject { ["tipo"] = "TIPO_INEXISTENTE", ["quando"] = null };
        IReadOnlyList<FieldError> causas = Conferir(Padrao, campo, (Item + ".exibicao[0][0].valor", CondicaoIncoerente), (null, Malformado));
        causas[1].Error.Message.Should().Contain($"{Item}.obrigatoriedade.tipo");
    }

    [Fact]
    public void LerFatosColetados_RestricaoRecusadaEDepoisRestricaoForaDoFormato_EncerraSemLerOImpedimento()
    {
        JsonObject campo = Campo();
        campo["restricoes"] = new JsonArray(Tamanho(5, 2), new JsonObject { ["tipo"] = "TIPO_INEXISTENTE" });
        campo["impedimento"] = ImpedimentoComCondicaoInvalida();
        IReadOnlyList<FieldError> causas = Conferir(Padrao, campo, (Item + ".restricoes[0]", "RestricaoValor.LimitesIncoerentes"), (null, Malformado));
        causas[1].Error.Message.Should().Contain($"{Item}.restricoes[1].tipo");
    }

    [Theory]
    [InlineData("hipoteseLegal", "HIPOTESE_FORA", "'dadosDosFormularios[0].hipoteseLegal' fora do vocabulário de hipóteses legais.")]
    [InlineData("formularioCodigo", "OUTRO_FORMULARIO", "'dadosDosFormularios[0].formularioCodigo' não é o código de um formulário do documento.")]
    [InlineData("componenteExibicao", " COMPONENTE_FORA", "'dadosDosFormularios[0].componenteExibicao' não está na forma canônica: sem espaço nas pontas e não vazio.")]
    public void LerFatosColetados_ItemComUmSoDefeitoDeVocabulario_DevolveCodigoETextoDeSempre(string chave, string valor, string mensagem)
    {
        JsonObject campo = Campo();
        campo[chave] = valor;
        IReadOnlyList<FieldError> causas = Conferir(Padrao, campo, ($"{Item}.{chave}", Malformado));
        causas[0].Error.Message.Should().Be(mensagem);
    }

    [Fact]
    public void LerFatosColetados_ImpedimentoForaDaInscricaoSozinho_DevolveCodigoETextoDeSempre()
    {
        JsonObject campo = Campo();
        campo["tipoDeCampo"] = "SIM_OU_NAO";
        campo["formato"] = null;
        campo["formularioCodigo"] = FormulariosDoDocumentoDeTeste.CodigoDaHabilitacao;
        campo["impedimento"] = new JsonObject { ["mensagem"] = "Mensagem", ["quando"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create(true)))) };
        IReadOnlyList<FieldError> causas = Conferir(FormulariosDoDocumentoDeTeste.ComFormularioForaDaInscricao(), campo, (Item + ".impedimento", Malformado));
        causas[0].Error.Message.Should().Be($"'{Item}.impedimento': Só o formulário de inscrição tem impedimento: ele impede a inscrição do candidato.");
    }

    [Fact]
    public void LerFatosColetados_ExibicaoQueCitaOProprioDado_RecusaPeloDominioNoItem()
    {
        JsonObject campo = Campo();
        campo["exibicao"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create("A"))));
        Conferir(Padrao, campo, (Item, ItemFormularioErrorCodes.RegraAutorreferente));
    }

    private static FormulariosDoDocumento Padrao => FormulariosDoDocumentoDeTeste.ComFormularios();

    private static JsonObject Campo() => GrupoNoEnvelope.Campo();

    private static JsonObject ComExibicaoInvalida()
    {
        JsonObject campo = Campo();
        campo["exibicao"] = CondicoesNoEnvelope.Invalidas(false, true);
        return campo;
    }

    private static JsonObject Tamanho(int minimo, int maximo) => new() { ["tipo"] = "TAMANHO_TEXTO", ["minimo"] = minimo, ["maximo"] = maximo };

    private static JsonObject OpcoesComCondicaoInvalida() => new()
    {
        ["tipo"] = "OPCOES_PERMITIDAS",
        ["entradas"] = new JsonArray(new JsonObject { ["quando"] = CondicoesNoEnvelope.Invalidas(false, true), ["valores"] = new JsonArray("A") }),
    };

    private static JsonObject ImpedimentoComCondicaoInvalida() => new() { ["mensagem"] = "Mensagem", ["quando"] = CondicoesNoEnvelope.Invalidas(false, true) };

    private static IReadOnlyList<FieldError> Conferir(FormulariosDoDocumento formularios, JsonObject campo, params (string? Campo, string Codigo)[] esperadas)
    {
        JsonObject documento = new() { ["dadosDosFormularios"] = new JsonArray(campo) };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerFatosColetados(leitor, documento, formularios);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;

        causas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(esperadas.Select(static e => (e.Campo, e.Codigo)));
        causas.Should().OnlyHaveUniqueItems();
        causas.Where(static causa => causa.Field is not null).Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
        foreach (string valor in ValoresInjetados)
        {
            causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(valor) && (causa.Field == null || !causa.Field.Contains(valor)));
        }

        return causas;
    }
}
