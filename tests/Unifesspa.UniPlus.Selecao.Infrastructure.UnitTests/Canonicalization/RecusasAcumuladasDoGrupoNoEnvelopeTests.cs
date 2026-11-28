namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class RecusasAcumuladasDoGrupoNoEnvelopeTests
{
    private const string Grupo = "gruposDosFormularios[0]";
    private const string CodigoDoDadoRecusado = "EXEMPLO_DADO_RECUSADO";
    private static readonly DomainError CondicaoIncoerente = new("CondicaoDnf.FormaIncoerenteComOperador", "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.");
    private static readonly DomainError OrdemNegativa = new(GrupoFormularioErrorCodes.OrdemInvalida, "A ordem do grupo não pode ser negativa.");
    private static readonly DomainError SecaoPropria = new(GrupoFormularioErrorCodes.CampoComSecaoPropria, "O campo segue a seção do grupo e não declara seção própria.");

    [Fact]
    public void LerGruposColetados_ExibicaoInvalidaEOrdemNegativa_PropagaAmbasCadaUmaNoSeuCaminho()
    {
        JsonObject grupo = GrupoComExibicaoInvalida();
        grupo["ordem"] = -1;
        Conferir(grupo, [(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente), (Grupo + ".ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerGruposColetados_ExibicaoInvalidaELimiteDeOcorrenciasIncoerente_PropagaAmbas()
    {
        JsonObject grupo = GrupoComExibicaoInvalida();
        grupo["minimo"] = 3;
        grupo["maximo"] = 2;
        Conferir(grupo, [(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente), (Grupo, DoDominio(GrupoFormularioErrorCodes.ContagemIncoerente))]);
    }

    [Fact]
    public void LerGruposColetados_ExibicaoInvalidaECodigoForaDoFormato_PropagaAmbas()
    {
        JsonObject grupo = GrupoComExibicaoInvalida();
        grupo["codigo"] = "codigo-fora-do-formato";
        Conferir(grupo, [(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente), (Grupo + ".codigo", DoDominio(GrupoFormularioErrorCodes.CodigoInvalido))]);
    }

    [Fact]
    public void LerGruposColetados_ExibicaoEObrigatoriedadeInvalidas_PropagaAmbasCadaUmaNoSeuCaminho()
    {
        JsonObject grupo = GrupoComExibicaoInvalida();
        grupo["obrigatoriedade"] = new JsonObject { ["tipo"] = "QUANDO", ["quando"] = CondicoesNoEnvelope.Invalidas(false, true) };
        Conferir(grupo, [(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente), (Grupo + ".obrigatoriedade.quando[0][0].valor", CondicaoIncoerente)]);
    }

    [Fact]
    public void LerGruposColetados_SoObrigatoriedadeInvalida_NaoAcusaRegraQueCitaOGrupo()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        grupo["obrigatoriedade"] = new JsonObject { ["tipo"] = "QUANDO", ["quando"] = CondicoesNoEnvelope.Invalidas(false, true) };
        Conferir(grupo, [(Grupo + ".obrigatoriedade.quando[0][0].valor", CondicaoIncoerente)]);
    }

    [Fact]
    public void LerGruposColetados_SoExibicaoInvalida_NaoGeraRecusaExtra()
    {
        Conferir(GrupoComExibicaoInvalida(), [(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente)]);
    }

    [Fact]
    public void LerGruposColetados_ExibicaoInvalidaECampoRecusado_PropagaAsCausasDoGrupoAntesDasDoCampo()
    {
        JsonObject campo = GrupoNoEnvelope.Campo();
        campo["tipoDeCampo"] = "TIPO_INEXISTENTE";
        JsonObject grupo = GrupoNoEnvelope.Grupo(campo);
        grupo["exibicao"] = CondicoesNoEnvelope.Invalidas(false, true);
        JsonObject documento = GrupoNoEnvelope.Documento(grupo);
        IReadOnlyList<FieldError> causas = Ler(documento);

        causas.Should().HaveCount(2);
        causas[0].Should().Be(new FieldError(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente));
        causas[1].Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causas[1].Error.Message.Should().Contain("subitens[0].tipoDeCampo");
        causas[1].Error.Message.Should().NotContain("TIPO_INEXISTENTE");
    }

    [Fact]
    public void LerGruposColetados_CampoComSecaoPropria_RecusaUmaVezSoNoCampo()
    {
        JsonObject campo = GrupoNoEnvelope.Campo(CodigoDoDadoRecusado);
        campo["paginaCodigo"] = "OUTRA_SECAO";
        Conferir(GrupoNoEnvelope.Grupo(campo), [(Grupo + ".subitens[0].paginaCodigo", SecaoPropria)]);
    }

    [Fact]
    public void LerGruposColetados_CampoDeOutroFormulario_RecusaSemExcecaoEPodeSairComAExibicao()
    {
        JsonObject campo = GrupoNoEnvelope.Campo();
        campo["formularioCodigo"] = FormulariosDoDocumentoDeTeste.CodigoDaHabilitacao;
        JsonObject grupo = GrupoNoEnvelope.Grupo(campo);
        grupo["exibicao"] = CondicoesNoEnvelope.Invalidas(false, true);
        DomainError esperado = new(ErrosCodecEnvelope.EnvelopeMalformado, $"'{Grupo}.subitens': um campo tem formulário próprio, mas o campo do grupo segue o formulário do grupo.");
        Conferir(grupo, [(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente), (Grupo + ".subitens", esperado)]);
    }

    [Fact]
    public void LerGruposColetados_RegraQueCitaOGrupoComExibicaoValida_AcusaSemRepetirOCodigoDoDado()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        grupo["exibicao"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create("A"))));
        JsonObject documento = GrupoNoEnvelope.Documento(grupo);
        IReadOnlyList<FieldError> causas = Ler(documento);

        causas.Should().ContainSingle().Which.Error.Code.Should().Be(GrupoFormularioErrorCodes.RegraAutorreferente);
        causas[0].Error.Message.Should().NotContain("DADO");
        ConferirCausas(documento, causas);
    }

    [Fact]
    public void LerGruposColetados_TipoDaObrigatoriedadeForaDoFormatoComExibicaoInvalida_EncerraOItemComAExibicaoAntes()
    {
        JsonObject grupo = GrupoComExibicaoInvalida();
        grupo["obrigatoriedade"] = new JsonObject { ["tipo"] = "TIPO_INEXISTENTE", ["quando"] = null };
        IReadOnlyList<FieldError> causas = Ler(GrupoNoEnvelope.Documento(grupo));

        causas.Should().HaveCount(2);
        causas[0].Should().Be(new FieldError(Grupo + ".exibicao[0][0].valor", CondicaoIncoerente));
        causas[1].Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causas[1].Error.Message.Should().Contain("obrigatoriedade.tipo");
    }

    private static DomainError DoDominio(string codigo) => new(codigo, string.Empty);

    private static JsonObject GrupoComExibicaoInvalida()
    {
        JsonObject grupo = GrupoNoEnvelope.Grupo(GrupoNoEnvelope.Campo());
        grupo["exibicao"] = CondicoesNoEnvelope.Invalidas(false, true);
        return grupo;
    }

    private static IReadOnlyList<FieldError> Ler(JsonObject documento)
    {
        LeitorEnvelope leitor = new();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(StringComparer.Ordinal);
        LeituraDosGrupos.LerGruposColetados(leitor, documento, valores, [], FormulariosDoDocumentoDeTeste.ComFormularios());
        return leitor.Falha<object>().Errors;
    }

    private static void Conferir(JsonObject grupo, (string Campo, DomainError Erro)[] esperadas)
    {
        JsonObject documento = GrupoNoEnvelope.Documento(grupo);
        IReadOnlyList<FieldError> causas = Ler(documento);
        causas.Select(causa => (causa.Field!, causa.Error.Code)).Should().Equal(esperadas.Select(esperada => (esperada.Campo, esperada.Erro.Code)));
        causas.Select(static causa => causa.Error.Message).Zip(esperadas, static (mensagem, esperada) => esperada.Erro.Message.Length == 0 || mensagem == esperada.Erro.Message)
            .Should().AllBeEquivalentTo(true);
        ConferirCausas(documento, causas);
    }

    private static void ConferirCausas(JsonObject documento, IReadOnlyList<FieldError> causas)
    {
        causas.Should().OnlyHaveUniqueItems();
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
        foreach (string valor in new[] { "VALOR_FICTICIO_X", "codigo-fora-do-formato", CodigoDoDadoRecusado, "OUTRA_SECAO" })
        {
            causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(valor) && !causa.Field!.Contains(valor));
        }
    }
}
