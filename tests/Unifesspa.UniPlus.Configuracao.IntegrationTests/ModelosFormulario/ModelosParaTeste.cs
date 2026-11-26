namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Os corpos de cadastro de modelo de formulário que os testes enviam.</summary>
internal static class ModelosParaTeste
{
    /// <summary>
    /// Um modelo de habilitação com duas perguntas, a segunda exibida a quem respondeu sim à
    /// primeira, e o bloco de revisão e aceite que todo formulário exige.
    /// </summary>
    internal static object Habilitacao(string codigo, string fatoDoPrimeiroItem = "QUILOMBOLA") =>
        Montar(codigo, "Habilitação Medicina 2027", DeOutraFaseObrigatoria, "Habilitação",
        [
            Item(fatoDoPrimeiroItem, 0, "Quilombola", "SIM_OU_NAO"),
            Item("BAIXA_RENDA", 1, "Baixa renda", "SIM_OU_NAO", Condicao("QUILOMBOLA", true)),
        ]);

    /// <summary>
    /// Um modelo de inscrição com a pergunta de renda exibida a quem se declarou preto: cor ou raça
    /// vem da seção dos dados básicos, que o modelo não declara.
    /// </summary>
    internal static object Inscricao(string codigo) =>
        Montar(codigo, "Inscrição", DaInscricao, "Inscrição",
        [
            Item("BAIXA_RENDA", 0, "Baixa renda", "SIM_OU_NAO", Condicao("COR_RACA", "PRETA")),
        ]);

    /// <summary>
    /// Um modelo de inscrição com um campo de escolha cujas opções seguem a resposta anterior: a forma
    /// de conclusão do ensino médio admite a EJA só para quem se declarou quilombola.
    /// </summary>
    internal static object InscricaoComOpcoes(string codigo) =>
        Montar(codigo, "Inscrição com opções", DaInscricao, "Inscrição",
        [
            Item("QUILOMBOLA", 0, "Quilombola", "SIM_OU_NAO"),
            ItemComRestricoes("FORMA_CONCLUSAO_EM", 1, "Forma de conclusão do ensino médio", "ESCOLHA_UNICA", OpcoesPorQuilombola()),
        ]);

    /// <summary>Um modelo de inscrição que pergunta de qual recurso de acessibilidade o candidato vai precisar.</summary>
    internal static object InscricaoComRecursoDeAcessibilidade(string codigo) =>
        Montar(codigo, "Inscrição com recurso de acessibilidade", DaInscricao, "Inscrição",
        [
            Item("RECURSO_ACESSIBILIDADE", 0, "De qual recurso de acessibilidade você vai precisar?", "ESCOLHA_MULTIPLA", obrigatoriedade: "NUNCA"),
        ]);

    internal static object Edicao(string nome, JsonNode? conteudo) =>
        new { descritivo = new { nome, descricao = (string?)null, tipoProcessoCodigo = (string?)null }, conteudo };

    internal static Dictionary<string, object?> ComTipoDeProcesso(object modelo, string tipoProcessoCodigo)
    {
        Dictionary<string, object?> campos = new(JsonSerializer.SerializeToNode(modelo)!.AsObject()
            .Select(static p => KeyValuePair.Create(p.Key, (object?)p.Value?.DeepClone())));
        JsonObject descritivo = campos["descritivo"] is JsonObject atual ? atual : new JsonObject();
        descritivo["tipoProcessoCodigo"] = tipoProcessoCodigo;
        campos["descritivo"] = descritivo;
        return campos;
    }

    /// <summary>O mesmo corpo com a declaração trocada; sem valor, a declaração sai do corpo.</summary>
    internal static Dictionary<string, object?> ComDeclaracao(object modelo, JsonNode? declaracao)
    {
        Dictionary<string, object?> campos = new(JsonSerializer.SerializeToNode(modelo)!.AsObject()
            .Select(static p => KeyValuePair.Create(p.Key, (object?)p.Value?.DeepClone())));
        campos.Remove("declaracao");
        if (declaracao is not null)
        {
            campos["declaracao"] = declaracao;
        }

        return campos;
    }

    private static readonly object DaInscricao = new { serveAFaseDeInscricao = true, obrigatorio = true };

    private static readonly object DeOutraFaseObrigatoria = new { serveAFaseDeInscricao = false, obrigatorio = true };

    private static object Montar(string codigo, string nome, object declaracao, string titulo, object[] itens) =>
        new
        {
            codigo,
            declaracao,
            descritivo = new { nome, descricao = (string?)null, tipoProcessoCodigo = (string?)null },
            conteudo = new { titulo, paginas = Paginas(), itens },
        };

    private static object[] Paginas() =>
    [
        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, apresentacao = new { titulo = "Dados", descricao = (string?)null, aviso = (string?)null } },
        new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", apresentacao = new { titulo = "Revisão", descricao = (string?)null, aviso = (string?)null } },
    ];

    private static object Item(
        string fatoCodigo, int ordem, string rotulo, string tipoDeCampo, object? exibicao = null, string obrigatoriedade = "SEMPRE") =>
        new
        {
            fatoCodigo,
            ordem,
            paginaCodigo = "DADOS",
            apresentacao = new { rotulo, tipoDeCampo },
            regras = new { exibicao, obrigatoriedade = new { tipo = obrigatoriedade } },
        };

    private static object ItemComRestricoes(string fatoCodigo, int ordem, string rotulo, string tipoDeCampo, object restricoes) =>
        new
        {
            fatoCodigo,
            ordem,
            paginaCodigo = "DADOS",
            apresentacao = new { rotulo, tipoDeCampo },
            regras = new { exibicao = (object?)null, obrigatoriedade = new { tipo = "SEMPRE" }, restricoes },
        };

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
