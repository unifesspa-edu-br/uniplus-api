namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization.ApoioDoGrupoNoEnvelope;

/// <summary>O campo recusado na leitura não esconde o que está errado no próprio grupo, e não gera recusa que dependa dos campos.</summary>
public sealed class CampoRecusadoNoGrupoDoEnvelopeTests
{
    private const string ValorDoTipoInexistente = "TIPO_INEXISTENTE";
    private const string CaminhoDoCampoRecusado = "subitens[0].tipoDeCampo";
    private static readonly string[] ValoresInjetados = [ValorDoTipoInexistente, "VALOR_FICTICIO_X", "OUTRO_FORMULARIO", "codigo-fora-do-formato", "OUTRA_SECAO"];

    [Fact]
    public void LerGruposColetados_SoCampoRecusado_DevolveUmaRecusaDoCampo()
    {
        ConferirComCampoRecusado(GrupoComCampoRecusado());
    }

    [Fact]
    public void LerGruposColetados_CampoRecusadoECodigoForaDoFormato_PropagaOCodigoAntesDoCampo()
    {
        JsonObject grupo = GrupoComCampoRecusado();
        grupo["codigo"] = "codigo-fora-do-formato";
        ConferirComCampoRecusado(grupo, (Grupo + ".codigo", GrupoFormularioErrorCodes.CodigoInvalido));
    }

    [Fact]
    public void LerGruposColetados_CampoRecusadoEOrdemNegativa_PropagaAOrdemAntesDoCampo()
    {
        JsonObject grupo = GrupoComCampoRecusado();
        grupo["ordem"] = -1;
        ConferirComCampoRecusado(grupo, (Grupo + ".ordem", GrupoFormularioErrorCodes.OrdemInvalida));
    }

    [Fact]
    public void LerGruposColetados_CampoRecusadoELimiteDeOcorrenciasIncoerente_PropagaOGrupoSemChaveAntesDoCampo()
    {
        JsonObject grupo = GrupoComCampoRecusado();
        grupo["minimo"] = 3;
        grupo["maximo"] = 2;
        ConferirComCampoRecusado(grupo, (Grupo, GrupoFormularioErrorCodes.ContagemIncoerente));
    }

    [Fact]
    public void LerGruposColetados_SoCodigoForaDoFormatoComCampoIntegro_DevolveUmaRecusa()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        grupo["codigo"] = "codigo-fora-do-formato";
        Conferir(grupo, [(Grupo + ".codigo", DoDominio(GrupoFormularioErrorCodes.CodigoInvalido))], ValoresInjetados);
    }

    [Fact]
    public void LerGruposColetados_SoOrdemNegativaComCampoIntegro_DevolveUmaRecusa()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        grupo["ordem"] = -1;
        Conferir(grupo, [(Grupo + ".ordem", OrdemNegativa)], ValoresInjetados);
    }

    [Fact]
    public void LerGruposColetados_SoLimiteDeOcorrenciasIncoerenteComCampoIntegro_DevolveUmaRecusa()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        grupo["minimo"] = 3;
        grupo["maximo"] = 2;
        Conferir(grupo, [(Grupo, DoDominio(GrupoFormularioErrorCodes.ContagemIncoerente))], ValoresInjetados);
    }

    [Fact]
    public void LerGruposColetados_CampoRecusadoComSecaoPropria_RecusaSoOCampo()
    {
        JsonObject campo = CampoRecusado();
        campo["paginaCodigo"] = "OUTRA_SECAO";
        ConferirComCampoRecusado(GrupoNoEnvelope.Grupo(campo));
    }

    [Fact]
    public void LerGruposColetados_CampoIntegroComSecaoPropriaAntesDeCampoRecusado_RecusaSoOCampoRecusado()
    {
        JsonObject integro = GrupoNoEnvelope.Campo("OUTRO_DADO");
        integro["paginaCodigo"] = "OUTRA_SECAO";
        IReadOnlyList<FieldError> causas = Ler(GrupoNoEnvelope.Documento(GrupoNoEnvelope.Grupo(integro, CampoRecusado())));

        causas.Should().ContainSingle().Which.Error.Message.Should().Contain("subitens[1].tipoDeCampo");
    }

    [Fact]
    public void LerGruposColetados_CampoRecusadoComImpedimentoDeclarado_RecusaSoOCampo()
    {
        JsonObject campo = CampoRecusado();
        campo["impedimento"] = new JsonObject
        {
            ["mensagem"] = "Mensagem",
            ["quando"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create(true)))),
        };
        ConferirComCampoRecusado(GrupoNoEnvelope.Grupo(campo));
    }

    [Fact]
    public void LerGruposColetados_GrupoQueIncluiOCandidatoSemParentescoECampoRecusado_RecusaSoOCampo()
    {
        JsonObject grupo = GrupoComCampoRecusado();
        grupo["incluiCandidato"] = true;
        ConferirComCampoRecusado(grupo);
    }

    [Fact]
    public void LerGruposColetados_ExibicaoQueCitaOCampoRecusado_RecusaSoOCampo()
    {
        JsonObject grupo = GrupoComCampoRecusado();
        grupo["exibicao"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create("A"))));
        ConferirComCampoRecusado(grupo);
    }

    [Fact]
    public void LerGruposColetados_TrintaEUmCamposComOPrimeiroRecusado_RecusaSoOCampo()
    {
        JsonObject[] campos = [CampoRecusado(), .. Enumerable.Range(1, FormaDoGrupo.MaximoDeSubitens).Select(static i => GrupoNoEnvelope.Campo($"DADO_{i}"))];
        ConferirComCampoRecusado(GrupoNoEnvelope.Grupo(campos));
    }

    [Fact]
    public void LerGruposColetados_DoisCamposRecusados_RecusaSoOPrimeiro()
    {
        JsonObject segundo = CampoRecusado();
        segundo["codigoDoDado"] = "OUTRO_DADO";
        ConferirComCampoRecusado(GrupoNoEnvelope.Grupo(CampoRecusado(), segundo));
    }

    [Fact]
    public void LerGruposColetados_OrdemDoCampoComoTextoEOrdemNegativaDoGrupo_DeixaAChaveDoCampoPorUltimo()
    {
        JsonObject campo = GrupoNoEnvelope.Campo();
        campo["ordem"] = "texto";
        JsonObject grupo = GrupoNoEnvelope.Grupo(campo);
        grupo["ordem"] = -1;
        IReadOnlyList<FieldError> causas = Ler(GrupoNoEnvelope.Documento(grupo));

        causas.Should().HaveCount(2);
        causas[0].Should().Be(new FieldError(Grupo + ".ordem", OrdemNegativa));
        causas[1].Field.Should().BeNull();
        causas[1].Error.Message.Should().Contain("subitens[0].ordem");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("texto"));
    }

    [Fact]
    public void LerGruposColetados_PrimeiroCampoQueNaoEObjetoECodigoInvalido_DeixaOFormatoPorUltimo()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo();
        grupo["subitens"] = new JsonArray(JsonValue.Create(5));
        grupo["codigo"] = "codigo-fora-do-formato";
        IReadOnlyList<FieldError> causas = Ler(GrupoNoEnvelope.Documento(grupo));

        causas.Should().HaveCount(2);
        causas[0].Field.Should().Be(Grupo + ".codigo");
        causas[0].Error.Code.Should().Be(GrupoFormularioErrorCodes.CodigoInvalido);
        causas[1].Field.Should().BeNull();
        causas[1].Error.Message.Should().Contain("subitens[0]");
    }

    [Fact]
    public void LerGruposColetados_FormularioDesconhecidoCampoRecusadoEOrdemNegativa_SaemNaOrdemDoGrupoEDoCampo()
    {
        JsonObject grupo = GrupoComCampoRecusado();
        grupo["formularioCodigo"] = "OUTRO_FORMULARIO";
        grupo["ordem"] = -1;
        ConferirComCampoRecusado(grupo, (Grupo + ".formularioCodigo", ErrosCodecEnvelope.EnvelopeMalformado), (Grupo + ".ordem", GrupoFormularioErrorCodes.OrdemInvalida));
    }

    [Fact]
    public void LerGruposColetados_CampoRecusadoNoPrimeiroGrupo_NaoLeOSegundoGrupo()
    {
        JsonObject segundo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        segundo["ordem"] = -1;
        JsonObject documento = GrupoNoEnvelope.Documento(GrupoComCampoRecusado());
        ((JsonArray)documento["gruposDosFormularios"]!).Add(segundo);
        IReadOnlyList<FieldError> causas = Ler(documento);

        causas.Should().ContainSingle().Which.Error.Message.Should().Contain(CaminhoDoCampoRecusado);
    }

    private static JsonObject CampoRecusado()
    {
        JsonObject campo = GrupoNoEnvelope.Campo();
        campo["tipoDeCampo"] = ValorDoTipoInexistente;
        return campo;
    }

    private static JsonObject GrupoComCampoRecusado() => GrupoNoEnvelope.Grupo(CampoRecusado());

    /// <summary>As recusas do grupo na ordem dada e, por último, a única recusa do campo, sem o valor do documento.</summary>
    private static void ConferirComCampoRecusado(JsonObject grupo, params (string Campo, string Codigo)[] doGrupo)
    {
        JsonObject documento = GrupoNoEnvelope.Documento(grupo);
        IReadOnlyList<FieldError> causas = Ler(documento);

        causas.Take(doGrupo.Length).Select(static causa => (causa.Field!, causa.Error.Code)).Should().Equal(doGrupo);
        causas.Should().HaveCount(doGrupo.Length + 1);
        causas[^1].Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causas[^1].Error.Message.Should().Contain(CaminhoDoCampoRecusado);
        ConferirCausas(documento, causas, ValoresInjetados);
    }
}
