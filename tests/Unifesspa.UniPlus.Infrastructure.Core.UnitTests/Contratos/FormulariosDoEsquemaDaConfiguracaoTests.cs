namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// Os formulários, os itens, os grupos, os termos e as regras de derivação no esquema da configuração: o item cita o fato
/// pelo código, o termo cita o termo e a versão do catálogo pelo código estável e pelo número, e o que o catálogo fornece
/// não se declara no documento.
/// </summary>
public sealed class FormulariosDoEsquemaDaConfiguracaoTests
{
    private const string Formulario = "formulario.schema.json";

    private const string Coleta = "coleta.schema.json";

    private const string FormularioValido = """
        {
          "finalidade": "INSCRICAO", "faseChave": "inscricao", "titulo": "Inscrição",
          "paginas": [
            { "codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "titulo": "Dados" },
            { "codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE" }
          ],
          "itens": [
            { "codigoDoDado": "RENDA_PER_CAPITA", "paginaCodigo": "DADOS", "ordem": 0, "rotulo": "Renda",
              "obrigatoriedade": { "tipo": "SEMPRE" },
              "restricoes": [ { "tipo": "FAIXA_NUMERICA", "minimo": 0, "maximo": 100000.5 } ],
              "impedimento": { "quando": [[ { "codigoDoDado": "RENDA_PER_CAPITA", "operador": "MAIOR_IGUAL", "valor": 5000 } ]], "mensagem": "Renda acima do limite." } },
            { "codigoDoDado": "SITUACAO", "paginaCodigo": "DADOS", "ordem": 1, "opcoes": [ { "codigo": "A", "rotulo": "Opção A" } ] }
          ],
          "grupos": [
            { "codigo": "FAMILIA", "paginaCodigo": "DADOS", "ordem": 2, "rotulo": "Família", "minimo": 0, "maximo": null, "incluiCandidato": true,
              "obrigatoriedade": { "tipo": "NUNCA" },
              "subitens": [ { "codigoDoDado": "NOME_MEMBRO", "ordem": 0 } ] }
          ],
          "termos": [
            { "codigo": "LGPD", "ordem": 0, "termoCodigo": "TERMO_LGPD", "versaoNumero": 2, "obrigatoriedade": { "tipo": "SEMPRE" } }
          ]
        }
        """;

    private static JsonNode Alterado(string ponteiro, JsonNode? valor)
    {
        JsonNode formulario = Documento(FormularioValido);
        Atribuir(formulario, ponteiro, valor!);
        return formulario;
    }

    [Fact(DisplayName = "O formulário completo, com termo pelo código e pelo número da versão, valida")]
    public void Formulario_Completo_Valida() =>
        CaminhosComErro(Documento(FormularioValido), Formulario).Should().BeEmpty();

    [Fact(DisplayName = "O formulário fica no documento")]
    public void Documento_ComFormularios_Valida()
    {
        JsonNode documento = Documento("""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1 }""");
        documento["formularios"] = new JsonArray(Documento(FormularioValido));

        CaminhosComErro(documento).Should().BeEmpty();
    }

    [Theory(DisplayName = "O termo sem o código do termo ou sem o número da versão é recusado")]
    [InlineData("termoCodigo")]
    [InlineData("versaoNumero")]
    public void Termo_SemReferencia_Recusa(string propriedade)
    {
        JsonNode formulario = Documento(FormularioValido);
        formulario["termos"]![0]!.AsObject().Remove(propriedade);

        CaminhosComErro(formulario, Formulario).Should().Contain("/termos/0");
    }

    [Theory(DisplayName = "O código do termo fora do formato e o número de versão inválido são recusados")]
    [InlineData("termoCodigo", "termo lgpd", "/termos/0/termoCodigo")]
    [InlineData("termoCodigo", "3F2504E0-4F89-11D3-9A0C-0305E82C3301", "/termos/0/termoCodigo")]
    [InlineData("versaoNumero", 0, "/termos/0/versaoNumero")]
    [InlineData("versaoNumero", "2", "/termos/0/versaoNumero")]
    public void Termo_ReferenciaForaDoFormato_Recusa(string propriedade, object valor, string caminho)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        JsonNode formulario = Documento(FormularioValido);
        formulario["termos"]![0]![propriedade] = JsonValue.Create(valor);

        CaminhosComErro(formulario, Formulario).Should().Contain(caminho);
    }

    [Fact(DisplayName = "O nome, o texto e o hash do termo, que são da versão do catálogo, não se declaram")]
    public void Termo_CopiaDoCatalogo_Recusa()
    {
        JsonNode formulario = Documento(FormularioValido);
        formulario["termos"]![0]!["texto"] = "Texto legal";

        CaminhosComErro(formulario, Formulario).Should().Contain("/termos/0/texto");
    }

    [Theory(DisplayName = "A finalidade, o bloco ou a fase fora do vocabulário e do formato são recusados")]
    [InlineData("/finalidade", "MATRICULA", "/finalidade")]
    [InlineData("/paginas/1/bloco", "OUTRO_BLOCO", "/paginas/1/bloco")]
    [InlineData("/faseChave", "Fase Um", "/faseChave")]
    public void Formulario_ForaDoVocabulario_Recusa(string ponteiro, string valor, string caminho) =>
        CaminhosComErro(Alterado(ponteiro, valor), Formulario).Should().Contain(caminho);

    [Theory(DisplayName = "O subitem de grupo com página ou impedimento preenchidos é recusado")]
    [InlineData("paginaCodigo", "\"DADOS\"")]
    [InlineData("impedimento", """{ "quando": [[ { "codigoDoDado": "F", "operador": "IGUAL", "valor": true } ]], "mensagem": "Não." }""")]
    public void Subitem_ComPaginaOuImpedimento_Recusa(string propriedade, string valor)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        ArgumentNullException.ThrowIfNull(valor);
        JsonNode formulario = Documento(FormularioValido);
        formulario["grupos"]![0]!["subitens"]![0]![propriedade] = Documento(valor);

        CaminhosComErro(formulario, Formulario).Should().Contain("/grupos/0/subitens/0/" + propriedade);
    }

    [Fact(DisplayName = "O subitem com página e impedimento nulos, como o envelope os escreve, valida")]
    public void Subitem_ComPaginaEImpedimentoNulos_Valida()
    {
        JsonNode formulario = Documento(FormularioValido);
        formulario["grupos"]![0]!["subitens"]![0]!["paginaCodigo"] = null;
        formulario["grupos"]![0]!["subitens"]![0]!["impedimento"] = null;

        CaminhosComErro(formulario, Formulario).Should().BeEmpty();
    }

    [Fact(DisplayName = "O rascunho com grupo sem subitens ou seção com bloco valida; mais de trinta subitens é recusado")]
    public void Grupo_QuantidadeDeSubitens()
    {
        JsonNode rascunho = Alterado("/grupos/0/subitens", new JsonArray());
        rascunho["paginas"]![0]!["bloco"] = "COMPROVACAO_DOCUMENTAL";
        JsonNode excesso = Alterado("/grupos/0/subitens", new JsonArray([.. Enumerable.Range(0, 31).Select(static i => (JsonNode)Documento($$"""{ "codigoDoDado": "F{{i}}" }"""))]));

        CaminhosComErro(rascunho, Formulario).Should().BeEmpty();
        CaminhosComErro(excesso, Formulario).Should().Contain("/grupos/0/subitens");
    }

    [Theory(DisplayName = "A propriedade do catálogo copiada no item é recusada")]
    [InlineData("tipoDeCampo", "NUMERO")]
    [InlineData("classificacaoProtecao", "SENSIVEL")]
    [InlineData("formato", "CPF")]
    public void Item_CopiaDoCatalogo_Recusa(string propriedade, string valor)
    {
        JsonNode formulario = Documento(FormularioValido);
        formulario["itens"]![0]![propriedade] = valor;

        CaminhosComErro(formulario, Formulario).Should().Contain($"/itens/0/{propriedade}");
    }

    [Theory(DisplayName = "A restrição com tipo desconhecido ou a data com âncora inválida é recusada")]
    [InlineData("""{ "tipo": "REGEX" }""", "/itens/0/restricoes/0/tipo")]
    [InlineData("""{ "tipo": "FAIXA_DE_DATA", "dataMinima": { "referenciaDaData": "HOJE" } }""", "/itens/0/restricoes/0/dataMinima/referenciaDaData")]
    [InlineData("""{ "tipo": "FAIXA_DE_DATA", "dataMaxima": { "referenciaDaData": "ENVIO", "anos": 151 } }""", "/itens/0/restricoes/0/dataMaxima/anos")]
    public void Restricao_Invalida_Recusa(string restricao, string caminho)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        JsonNode formulario = Documento(FormularioValido);
        formulario["itens"]![0]!["restricoes"] = new JsonArray(Documento(restricao));

        CaminhosComErro(formulario, Formulario).Should().Contain(caminho);
    }

    [Fact(DisplayName = "A obrigatoriedade QUANDO sem predicado é recusada no item")]
    public void Item_ObrigatoriedadeQuandoSemPredicado_Recusa() =>
        CaminhosComErro(Alterado("/itens/0/obrigatoriedade", Documento("""{ "tipo": "QUANDO" }""")), Formulario)
            .Should().Contain("/itens/0/obrigatoriedade");

    [Fact(DisplayName = "As regras de derivação com regra âncora e regra condicionada validam")]
    public void RegrasDeDerivacao_Valida() =>
        CaminhosComErro(Documento("""
            { "calculosDosDados": [ { "codigoDoDado": "RENDA_FAIXA", "regras": [
                { "ordem": 0, "contribui": "ATE_1", "quando": [[ { "codigoDoDado": "RENDA_PER_CAPITA", "operador": "MENOR_IGUAL", "valor": 1000 } ]] },
                { "ordem": 1, "contribui": "ACIMA" } ] } ] }
            """), Coleta).Should().BeEmpty();

    [Fact(DisplayName = "A derivação sem o fato ou com valor contribuído acima de sessenta caracteres é recusada")]
    public void RegrasDeDerivacao_Invalida_Recusa()
    {
        CaminhosComErro(Documento("""{ "calculosDosDados": [ { "regras": [] } ] }"""), Coleta).Should().Contain("/calculosDosDados/0");
        CaminhosComErro(Documento($$"""{ "calculosDosDados": [ { "codigoDoDado": "F", "regras": [ { "contribui": "{{new string('A', 61)}}" } ] } ] }"""), Coleta)
            .Should().Contain("/calculosDosDados/0/regras/0/contribui");
    }
}
