namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Os corpos de cadastro de modelo de formulário que os testes enviam.</summary>
internal static class ModelosParaTeste
{
    /// <summary>
    /// Um modelo de habilitação com duas perguntas, a segunda exibida a quem respondeu sim à
    /// primeira, e o bloco de revisão e aceite que a finalidade exige.
    /// </summary>
    internal static object Habilitacao(string codigo, string fatoDoPrimeiroItem = "QUILOMBOLA") =>
        Montar(codigo, "Habilitação Medicina 2027", "HABILITACAO", "Habilitação",
        [
            Item(fatoDoPrimeiroItem, 0, "Quilombola", "BOOLEANO"),
            Item("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", Condicao("QUILOMBOLA", true)),
        ]);

    /// <summary>
    /// Um modelo de inscrição com a pergunta de renda exibida a quem se declarou preto: cor ou raça
    /// vem da seção dos dados básicos, que o modelo não declara.
    /// </summary>
    internal static object Inscricao(string codigo) =>
        Montar(codigo, "Inscrição", "INSCRICAO", "Inscrição",
        [
            Item("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", Condicao("COR_RACA", "PRETA")),
        ]);

    /// <summary>
    /// Um modelo de inscrição com um campo de escolha cujas opções seguem a resposta anterior: a forma
    /// de conclusão do ensino médio admite a EJA só para quem se declarou quilombola.
    /// </summary>
    internal static object InscricaoComOpcoes(string codigo) =>
        Montar(codigo, "Inscrição com opções", "INSCRICAO", "Inscrição",
        [
            Item("QUILOMBOLA", 0, "Quilombola", "BOOLEANO"),
            ItemComRestricoes("FORMA_CONCLUSAO_EM", 1, "Forma de conclusão do ensino médio", "SELECAO_UNICA", OpcoesPorQuilombola()),
        ]);

    /// <summary>Um modelo de inscrição que pergunta de qual recurso de acessibilidade o candidato vai precisar.</summary>
    internal static object InscricaoComRecursoDeAcessibilidade(string codigo) =>
        Montar(codigo, "Inscrição com recurso de acessibilidade", "INSCRICAO", "Inscrição",
        [
            Item("RECURSO_ACESSIBILIDADE", 0, "De qual recurso de acessibilidade você vai precisar?", "SELECAO_MULTIPLA", obrigatoriedade: "NUNCA"),
        ]);

    internal static object Edicao(string nome, JsonNode? conteudo) =>
        new { nome, descricao = (string?)null, tipoProcessoCodigo = (string?)null, conteudo };

    internal static Dictionary<string, object?> ComTipoDeProcesso(object modelo, string tipoProcessoCodigo)
    {
        Dictionary<string, object?> campos = new(JsonSerializer.SerializeToNode(modelo)!.AsObject()
            .Select(static p => KeyValuePair.Create(p.Key, (object?)p.Value?.DeepClone())));
        campos["tipoProcessoCodigo"] = tipoProcessoCodigo;
        return campos;
    }

    private static object Montar(string codigo, string nome, string finalidade, string titulo, object[] itens) =>
        new { codigo, nome, finalidade, conteudo = new { titulo, etapas = Etapas(), itens } };

    private static object[] Etapas() =>
    [
        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, titulo = "Dados", descricao = (string?)null, aviso = (string?)null },
        new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", titulo = "Revisão", descricao = (string?)null, aviso = (string?)null },
    ];

    private static object Item(
        string fatoCodigo, int ordem, string rotulo, string tipoRenderizacao, object? precondicao = null, string obrigatoriedade = "SEMPRE") =>
        new { fatoCodigo, ordem, rotulo, tipoRenderizacao, obrigatoriedade, precondicao, etapaCodigo = "DADOS" };

    private static object ItemComRestricoes(string fatoCodigo, int ordem, string rotulo, string tipoRenderizacao, object restricoes) =>
        new { fatoCodigo, ordem, rotulo, tipoRenderizacao, obrigatoriedade = "SEMPRE", precondicao = (object?)null, etapaCodigo = "DADOS", restricoes };

    private static object[][] Condicao(string fato, object valor) =>
        [[new { fato, operador = "IGUAL", valor }]];

    private static object[] OpcoesPorQuilombola() =>
    [
        new
        {
            tipo = "OPCOES_PERMITIDAS",
            entradas = new object[]
            {
                new { quando = (object?)null, valores = new[] { "REGULAR" } },
                new { quando = Condicao("QUILOMBOLA", true), valores = new[] { "EJA" } },
            },
        },
    ];
}
