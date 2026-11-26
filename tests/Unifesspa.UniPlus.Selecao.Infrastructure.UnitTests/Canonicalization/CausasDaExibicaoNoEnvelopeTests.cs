namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class CausasDaExibicaoNoEnvelopeTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerFatosColetados_ExibicaoInvalida_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject fato = Fato();
        fato["exibicao"] = CondicoesNoEnvelope.Invalidas(separadas, unica);
        JsonObject documento = new() { ["dadosDosFormularios"] = new JsonArray(fato) };
        LeitorEnvelope leitor = new();
        LeituraDosFatos.LerFatosColetados(leitor, documento, FormulariosDoDocumentoDeTeste.ComFormularios());
        CondicoesNoEnvelope.Conferir(documento, leitor, "dadosDosFormularios[0].exibicao", separadas, unica);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerGruposColetados_ExibicaoInvalida_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject grupo = Grupo();
        grupo["exibicao"] = CondicoesNoEnvelope.Invalidas(separadas, unica);
        JsonObject documento = new() { ["gruposDosFormularios"] = new JsonArray(grupo) };
        LeitorEnvelope leitor = new();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(StringComparer.Ordinal);
        LeituraDosGrupos.LerGruposColetados(leitor, documento, valores, [], FormulariosDoDocumentoDeTeste.ComFormularios());
        CondicoesNoEnvelope.Conferir(documento, leitor, "gruposDosFormularios[0].exibicao", separadas, unica);
    }

    private static JsonObject Fato() => JsonNode.Parse("""
        {"ajuda":null,"classificacaoProtecao":"PESSOAL","codigoDoDado":"DADO","componenteExibicao":null,
        "descricao":"Descrição","exibicao":null,"formularioCodigo":"INSCRICAO","finalidadeTratamento":"Finalidade",
        "formato":"LIVRE","hipoteseLegal":"CUMPRIMENTO_OBRIGACAO_LEGAL","impedimento":null,
        "obrigatoriedade":{"quando":null,"tipo":"SEMPRE"},"ordem":0,"origemValores":"CATALOGO",
        "paginaCodigo":"PAGINA","pedirConfirmacao":false,"restricoes":[],"rotulo":"Dado","tipoDeCampo":"TEXTO","valoresSelecionaveis":null}
        """)!.AsObject();

    private static JsonObject Grupo() => JsonNode.Parse("""
        {"agregados":[],"codigo":"GRUPO","exibicao":null,"formularioCodigo":"INSCRICAO","incluiCandidato":true,
        "maximo":null,"minimo":1,"obrigatoriedade":{"quando":null,"tipo":"NUNCA"},"ordem":0,
        "paginaCodigo":"PAGINA","rotulo":"Grupo","subitens":[]}
        """)!.AsObject();
}
