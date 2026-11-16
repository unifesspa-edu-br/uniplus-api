namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// As exigências de documentos e a árvore de satisfação no esquema da configuração: a exigência cita o tipo de documento
/// pelo código e a fase e a etapa pela chave local, o que vem do catálogo é só de leitura e a árvore recusa o que não é nó.
/// </summary>
public sealed class DocumentosExigidosDoEsquemaDaConfiguracaoTests
{
    private const string Exigencias = "exigencias.schema.json";

    private const string ExigenciaCompleta = """
        { "chave": "comprovante-de-renda", "tipoDocumentoCodigo": "COMPROVANTE_RENDA",
          "faseChave": "inscricao", "etapaChave": "prova-objetiva", "finalidade": "ISENCAO_TAXA",
          "aplicabilidade": "Condicional", "obrigatorio": true, "consequenciaIndeferimento": "PENDENCIA_REENVIO",
          "condicaoGatilho": [ [ { "fato": "RENDA_FAMILIAR", "operador": "MENOR_IGUAL", "valor": 3 } ] ],
          "basesLegais": [ { "referencia": "Decreto 6.593/2008", "abrangencia": "FEDERAL", "status": "RESOLVIDO" } ],
          "idadeMaximaEmissao": { "valor": 90, "unidade": "DIAS", "referenciaTipo": "FIM_INSCRICAO" },
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

    [Fact(DisplayName = "A idade máxima de emissão exige valor positivo e unidade conhecida")]
    public void IdadeMaximaDeEmissao_ValorEUnidade()
    {
        string definicao = $"{Exigencias}#/$defs/idadeMaximaEmissao";

        CaminhosComErro(Documento("""{ "valor": 0, "unidade": "SEMANAS" }"""), definicao).Should().Contain(["/valor", "/unidade"]);
        CaminhosComErro(Documento("""{ "valor": 6, "unidade": "MESES", "referenciaTipo": "INICIO_FASE", "referenciaFaseChave": "inscricao" }"""), definicao)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A árvore aceita grupo com folhas aninhadas e recusa tipo de nó desconhecido")]
    public void Arvore_GrupoComFolhas()
    {
        const string definicao = "arvore-de-satisfacao.schema.json";
        JsonNode grupo = Documento("""
            { "tipo": "OU", "ordem": 1, "quantidadeMinima": 1, "filhos": [
              { "tipo": "FOLHA", "ordem": 1, "exigenciaChave": "comprovante-de-renda", "chaveDistincao": "COMPETENCIA_MENSAL", "dataReferencia": "2027-01-10" },
              { "tipo": "E", "ordem": 2, "filhos": [ { "tipo": "FOLHA", "ordem": 1, "exigenciaChave": "declaracao" } ] } ] }
            """);

        CaminhosComErro(grupo, definicao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "XOR" }"""), definicao).Should().Contain("/tipo");
        CaminhosComErro(Documento("""{ "tipo": "OU", "filhos": [ { "tipo": "FOLHA", "exigenciaChave": "Comprovante Renda" } ] }"""), definicao)
            .Should().Contain("/filhos/0/exigenciaChave");
    }

    [Fact(DisplayName = "A seção de documentos exigidos fica no documento e recusa propriedade desconhecida")]
    public void Documento_ComDocumentosExigidos()
    {
        JsonNode documento = Documento($$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "documentosExigidos": { "exigencias": [ {{ExigenciaCompleta}} ], "arvoreDeSatisfacao": [ { "tipo": "FOLHA", "exigenciaChave": "comprovante-de-renda" } ] } }
            """);
        CaminhosComErro(documento).Should().BeEmpty();

        documento["documentosExigidos"]!["exigencias"]![0]!["campoInexistente"] = "valor";
        CaminhosComErro(documento).Should().Contain("/documentosExigidos/exigencias/0/campoInexistente");
    }

    [Fact(DisplayName = "A árvore aceita a ordem zero, a repetição por código de grupo e a folha até 366; recusa a folha acima")]
    public void Arvore_OrdemRepeticaoECardinalidade()
    {
        const string definicao = "arvore-de-satisfacao.schema.json";

        CaminhosComErro(Documento("""{ "tipo": "FOLHA", "ordem": 0, "exigenciaChave": "x", "quantidadeMinima": 366, "repetePorEntidade": "INTEGRANTE_FAMILIA" }"""), definicao)
            .Should().BeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "FOLHA", "quantidadeMinima": 367 }"""), definicao).Should().Contain("/quantidadeMinima");
        CaminhosComErro(Documento("""{ "tipo": "OU", "quantidadeMinima": 400, "filhos": [] }"""), definicao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "FOLHA", "repetePorEntidade": true }"""), definicao).Should().Contain("/repetePorEntidade");
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
