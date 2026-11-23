namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// As exigências de documentos no esquema da configuração citam o tipo de documento pelo código e as fases e etapas
/// pela chave local; os dados do catálogo são somente de leitura.
/// </summary>
public sealed class DocumentosExigidosDoEsquemaDaConfiguracaoTests
{
    private const string Exigencias = "exigencias.schema.json";

    private const string ExigenciaCompleta = """
        { "chave": "comprovante-de-renda", "tipoDocumentoCodigo": "COMPROVANTE_RENDA",
          "faseChave": "inscricao", "etapaChave": "prova-objetiva", "finalidade": "ISENCAO_TAXA",
          "aplicabilidade": "Condicional", "obrigatorio": true, "consequenciaIndeferimento": "PENDENCIA_REENVIO",
          "condicoes": [ [ { "codigoDoDado": "RENDA_FAMILIAR", "operador": "MENOR_IGUAL", "valor": 3 } ] ],
          "basesLegais": [ { "referencia": "Decreto 6.593/2008", "abrangencia": "FEDERAL", "situacao": "RESOLVIDO" } ],
          "prazoDeEmissao": { "escolha": "COM_PRAZO", "valor": 90, "unidade": "DIAS", "referencia": { "tipo": "FIM_DAS_INSCRICOES" }, "efeitoForaDoPrazo": "RECUSAR_DOCUMENTO" },
          "validade": { "escolha": "NAO_EXIGE_DOCUMENTO_VALIDO" },
          "formatosPermitidos": { "qualquer": false, "lista": [ { "formato": "PDF", "tamanhoMaximoBytesMax": 5242880 } ] },
          "tamanhoMaximoBytes": 5242880 }
        """;

    [Fact(DisplayName = "A exigência completa valida e a só com a chave, como num rascunho, também")]
    public void Exigencia_CompletaEApenasComAChave_Valida()
    {
        CaminhosComErro(Documento(ExigenciaCompleta), $"{Exigencias}#/$defs/exigencia").Should().BeEmpty();
        CaminhosComErro(Documento("""{ "chave": "rascunho" }"""), $"{Exigencias}#/$defs/exigencia").Should().BeEmpty();
    }

    [Fact(DisplayName = "A exigência sem chave é recusada")]
    public void Exigencia_SemChave_Recusa() =>
        CaminhosComErro(Documento("""{ "tipoDocumentoCodigo": "RG" }"""), $"{Exigencias}#/$defs/exigencia").Should().Contain("");

    [Theory(DisplayName = "O valor fora do vocabulário fechado da exigência é recusado com o caminho")]
    [InlineData("aplicabilidade", "\"Qualquer\"")]
    [InlineData("consequenciaIndeferimento", "\"ANULA\"")]
    [InlineData("finalidade", "\"OUTRA\"")]
    [InlineData("classificacaoProtecao", "\"SECRETO\"")]
    [InlineData("tamanhoMaximoBytes", "0")]
    public void Exigencia_ValorForaDoVocabulario_Recusa(string propriedade, string valor)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        ArgumentNullException.ThrowIfNull(valor);
        JsonNode exigencia = Documento($$"""{ "chave": "x", "{{propriedade}}": {{valor}} }""");

        CaminhosComErro(exigencia, $"{Exigencias}#/$defs/exigencia").Should().Contain($"/{propriedade}");
    }

    [Fact(DisplayName = "O código do tipo de documento com a forma de identificador interno é recusado")]
    public void Exigencia_TipoDocumentoPorIdentificadorInterno_Recusa() =>
        CaminhosComErro(Documento($$"""{ "chave": "x", "tipoDocumentoCodigo": "{{Guid.CreateVersion7()}}" }"""), $"{Exigencias}#/$defs/exigencia")
            .Should().Contain("/tipoDocumentoCodigo");

    [Fact(DisplayName = "O prazo de emissão aceita valor positivo e unidade conhecida")]
    public void PrazoDeEmissao_ValorEUnidade()
    {
        string definicao = $"{Exigencias}#/$defs/prazoDeEmissao";

        CaminhosComErro(Documento("""{ "valor": 0, "unidade": "semanas" }"""), definicao).Should().Contain(["/valor", "/unidade"]);
        CaminhosComErro(Documento("""{ "valor": 6, "unidade": "MESES", "referencia": { "tipo": "INICIO_DA_FASE", "faseChave": "inscricao" } }"""), definicao)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra aceita grupos com itens e recusa forma desconhecida")]
    public void Regra_GrupoComItens()
    {
        const string definicao = "regras-de-comprovacao.schema.json";
        JsonNode grupo = Documento("""
            { "forma": "QUANTIDADE_MINIMA_DE_ITENS", "quantidadeMinima": 1, "itens": [
              { "forma": "DOCUMENTO", "exigenciaChave": "comprovante-de-renda", "criterioDeDistincao": "COMPETENCIA_MENSAL", "dataReferencia": "2027-01-10" },
              { "forma": "TODOS_OS_ITENS", "itens": [ { "forma": "DOCUMENTO", "exigenciaChave": "declaracao" } ] } ] }
            """);

        CaminhosComErro(grupo, definicao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "forma": "XOR" }"""), definicao).Should().Contain("/forma");
        CaminhosComErro(Documento("""{ "forma": "QUANTIDADE_MINIMA_DE_ITENS", "itens": [ { "forma": "DOCUMENTO", "exigenciaChave": "Comprovante Renda" } ] }"""), definicao)
            .Should().Contain("/itens/0/exigenciaChave");
    }

    [Fact(DisplayName = "A seção de documentos exigidos fica no documento e recusa propriedade desconhecida")]
    public void Documento_ComDocumentosExigidos()
    {
        JsonNode documento = Documento($$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "documentosExigidos": { "exigencias": [ {{ExigenciaCompleta}} ], "regrasDeComprovacao": [ { "forma": "DOCUMENTO", "exigenciaChave": "comprovante-de-renda" } ] } }
            """);
        CaminhosComErro(documento).Should().BeEmpty();

        documento["documentosExigidos"]!["exigencias"]![0]!["campoInexistente"] = "valor";
        CaminhosComErro(documento).Should().Contain("/documentosExigidos/exigencias/0/campoInexistente");
    }

    [Fact(DisplayName = "A regra aceita repetição por grupo e até 366 apresentações")]
    public void Regra_RepeticaoEQuantidadeDeApresentacoes()
    {
        const string definicao = "regras-de-comprovacao.schema.json";

        CaminhosComErro(Documento("""{ "forma": "DOCUMENTO", "exigenciaChave": "x", "quantidadeMinima": 366, "grupoDoFormularioParaRepeticao": "INTEGRANTE_FAMILIA" }"""), definicao)
            .Should().BeEmpty();
        CaminhosComErro(Documento("""{ "forma": "DOCUMENTO", "quantidadeMinima": 367 }"""), definicao).Should().Contain("/quantidadeMinima");
        CaminhosComErro(Documento("""{ "forma": "QUANTIDADE_MINIMA_DE_ITENS", "quantidadeMinima": 400, "itens": [] }"""), definicao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "forma": "DOCUMENTO", "grupoDoFormularioParaRepeticao": true }"""), definicao).Should().Contain("/grupoDoFormularioParaRepeticao");
    }

    [Fact(DisplayName = "O modelo da exigência é aceito como descritivo e o código e a hipótese acima do limite do armazenamento são recusados")]
    public void Exigencia_ModeloELimitesDoArmazenamento()
    {
        string definicao = $"{Exigencias}#/$defs/exigencia";
        string longo = new('A', 61);

        CaminhosComErro(Documento("""{ "chave": "x", "modelo": { "nomeArquivo": "modelo.docx", "formato": "docx", "hashSha256": "0000000000000000000000000000000000000000000000000000000000000000" } }"""), definicao)
            .Should().BeEmpty();
        CaminhosComErro(Documento($$"""{ "chave": "x", "tipoDocumentoCodigo": "{{longo}}", "hipoteseLegal": "{{new string('h', 41)}}" }"""), definicao)
            .Should().Contain(["/tipoDocumentoCodigo", "/hipoteseLegal"]);
    }
}
