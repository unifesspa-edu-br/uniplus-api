namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CodigoDoGrupoNoEnvelopeTests
{
    [Theory]
    [InlineData(60, false)]
    [InlineData(61, true)]
    public void LerGruposColetados_TamanhoDoCodigo_RespeitaLimiteDoGrupo(int tamanho, bool recusado)
    {
        LeitorEnvelope leitor = new();
        string codigo = new('A', tamanho);
        IReadOnlyList<GrupoColetado> grupos = LeituraDosGrupos.LerGruposColetados(leitor, Envelope(codigo), new(StringComparer.Ordinal), []);

        leitor.Falhou.Should().Be(recusado, leitor.Falhou ? leitor.Falha<object>().Error!.Message : "");
        if (!recusado)
        {
            grupos.Should().ContainSingle().Which.Codigo.Should().Be(codigo);
        }
    }

    private static JsonObject Envelope(string codigo) => new()
    {
        ["gruposDosFormularios"] = new JsonArray(new JsonObject
        {
            ["codigo"] = codigo,
            ["finalidade"] = "INSCRICAO",
            ["paginaCodigo"] = "DADOS",
            ["ordem"] = 0,
            ["rotulo"] = "Grupo",
            ["minimo"] = 1,
            ["maximo"] = 2,
            ["incluiCandidato"] = false,
            ["exibicao"] = null,
            ["obrigatoriedade"] = new JsonObject { ["tipo"] = "SEMPRE", ["quando"] = null },
            ["subitens"] = new JsonArray(Campo()),
            ["agregados"] = new JsonArray(),
        }),
    };

    private static JsonObject Campo() => JsonNode.Parse("""
        {"codigoDoDado":"DADO","finalidade":"INSCRICAO","paginaCodigo":null,"ordem":0,"rotulo":"Dado",
         "tipoDeCampo":"TEXTO","obrigatoriedade":{"tipo":"SEMPRE","quando":null},"ajuda":null,
         "pedirConfirmacao":false,"restricoes":[],"impedimento":null,"origemValores":"CATALOGO","formato":"LIVRE",
         "classificacaoProtecao":"PESSOAL","hipoteseLegal":"CUMPRIMENTO_OBRIGACAO_LEGAL",
         "finalidadeTratamento":"Finalidade","descricao":"Descrição","componenteExibicao":null,
         "exibicao":null,"valoresSelecionaveis":null}
        """)!.AsObject();
}
