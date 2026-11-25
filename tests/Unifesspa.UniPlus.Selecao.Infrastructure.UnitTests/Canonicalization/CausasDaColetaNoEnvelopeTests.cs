namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaColetaNoEnvelopeTests
{
    [Fact]
    public void LerFatosColetados_OrdemNegativa_ApontaChavePublicada()
    {
        JsonObject fato = Fato();
        JsonObject documento = new() { ["dadosDosFormularios"] = new JsonArray(fato) };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerFatosColetados(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "dadosDosFormularios[0].ordem",
            "ItemFormulario.OrdemInvalida", "A ordem do item não pode ser negativa.");
    }

    [Fact]
    public void LerFatosColetados_ExibicaoAutorreferente_ApontaItem()
    {
        JsonObject fato = Fato();
        fato["ordem"] = 0;
        fato["exibicao"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create("SIM"))));
        JsonObject documento = new() { ["dadosDosFormularios"] = new JsonArray(fato) };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerFatosColetados(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "dadosDosFormularios[0]", "ItemFormulario.RegraAutorreferente", "A exibição cita o próprio fato.");
    }

    [Fact]
    public void LerGruposColetados_OcorrenciasESubitensInvalidos_PreservaCamposExistentes()
    {
        JsonObject grupo = Grupo();
        JsonObject documento = new() { ["gruposDosFormularios"] = new JsonArray(grupo) };
        LeitorEnvelope leitor = new();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(StringComparer.Ordinal);
        LeituraDosGrupos.LerGruposColetados(leitor, documento, valores, []);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "gruposDosFormularios[0]", "GrupoFormulario.ContagemIncoerente",
            "O mínimo de ocorrências não é negativo, e o máximo, quando declarado, é ao menos um e não fica abaixo do mínimo.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "gruposDosFormularios[0].subitens", "GrupoFormulario.SubitensForaDoLimite",
            "O grupo tem de um a 30 campos por ocorrência.");
    }

    [Fact]
    public void LerReferenciaTemporal_DataEFaseIncoerentes_PropagaAmbas()
    {
        JsonObject bloco = JsonNode.Parse("""{"referenciaTemporalDosDados":{"tipo":"FIM_INSCRICAO","data":"2026-01-01","faseId":"11111111-1111-1111-1111-111111111111"}}""")!.AsObject();
        JsonObject documento = new() { ["documentosExigidos"] = bloco };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerReferenciaTemporalFatosPolitica(leitor, bloco);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "documentosExigidos.referenciaTemporalDosDados.data", "ReferenciaTemporalFatos.DataIncoerenteComTipo", "A data só é aceita quando o tipo é DATA_ESPECIFICA.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "documentosExigidos.referenciaTemporalDosDados.faseId", "ReferenciaTemporalFatos.FaseIncoerenteComTipo", "A fase só é aceita quando o tipo é INICIO_FASE ou FIM_FASE.");
    }
    [Fact]
    public void LerGruposColetados_SemSubitens_ApontaColecao()
    {
        JsonObject grupo = Grupo();
        grupo["minimo"] = 1;
        JsonObject documento = new() { ["gruposDosFormularios"] = new JsonArray(grupo) };
        LeitorEnvelope leitor = new();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(StringComparer.Ordinal);
        LeituraDosGrupos.LerGruposColetados(leitor, documento, valores, []);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "gruposDosFormularios[0].subitens", "GrupoFormulario.SubitensForaDoLimite", "O grupo tem de um a 30 campos por ocorrência.");
    }

    [Fact]
    public void LerReferenciaTemporal_SemDataExigida_ApontaData()
    {
        JsonObject bloco = JsonNode.Parse("""{"referenciaTemporalDosDados":{"tipo":"DATA_ESPECIFICA","data":null,"faseId":null}}""")!.AsObject();
        JsonObject documento = new() { ["documentosExigidos"] = bloco };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerReferenciaTemporalFatosPolitica(leitor, bloco);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "documentosExigidos.referenciaTemporalDosDados.data", "ReferenciaTemporalFatos.DataIncoerenteComTipo", "DATA_ESPECIFICA exige a data.");
    }

    [Fact]
    public void LerFatosColetados_OrdemEFormatoInvalidos_PropagaAmbas()
    {
        JsonObject fato = Fato();
        fato["formato"] = null;
        JsonObject documento = new() { ["dadosDosFormularios"] = new JsonArray(fato) };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerFatosColetados(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "dadosDosFormularios[0].ordem", "ItemFormulario.OrdemInvalida", "A ordem do item não pode ser negativa.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "dadosDosFormularios[0].formato", "ItemFormulario.FormatoIncoerente", "O campo de texto tem o formato do fato no catálogo, com no máximo 30 caracteres; os demais campos não têm formato.");
    }

    private static JsonObject Fato() => JsonNode.Parse("""
        {"ajuda":null,"classificacaoProtecao":"PESSOAL","codigoDoDado":"DADO","componenteExibicao":null,
        "descricao":"Descrição","exibicao":null,"finalidade":"INSCRICAO","finalidadeTratamento":"Finalidade",
        "formato":"LIVRE","hipoteseLegal":"CUMPRIMENTO_OBRIGACAO_LEGAL","impedimento":null,
        "obrigatoriedade":{"quando":null,"tipo":"SEMPRE"},"ordem":-1,"origemValores":"CATALOGO",
        "paginaCodigo":"PAGINA","pedirConfirmacao":false,"restricoes":[],"rotulo":"Dado","tipoDeCampo":"TEXTO","valoresSelecionaveis":null}
        """)!.AsObject();

    private static JsonObject Grupo() => JsonNode.Parse("""
        {"agregados":[],"codigo":"GRUPO","exibicao":null,"finalidade":"INSCRICAO","incluiCandidato":false,
        "maximo":null,"minimo":-1,"obrigatoriedade":{"quando":null,"tipo":"NUNCA"},"ordem":0,
        "paginaCodigo":"PAGINA","rotulo":"Grupo","subitens":[]}
        """)!.AsObject();

}

internal static class ConferirCausaNoDocumento
{
    internal static void Conferir(JsonObject documento, FieldError causa, string campo, string codigo, string mensagem)
    {
        causa.Should().Be(new FieldError(campo, new DomainError(codigo, mensagem)));
        CaminhoNoDocumento.Existe(documento, campo).Should().BeTrue();
    }
}
