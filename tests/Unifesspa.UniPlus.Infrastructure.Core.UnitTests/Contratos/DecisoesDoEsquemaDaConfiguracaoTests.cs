namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// As decisões no esquema da configuração: os tipos de decisão da fase e da etapa, os motivos que o processo escolhe, a
/// regra de homologação das inscrições e os assinantes da fase, citados pelo identificador da conta e nunca por dado pessoal.
/// </summary>
public sealed class DecisoesDoEsquemaDaConfiguracaoTests
{
    private const string Comuns = "comuns.schema.json";
    private const string Cronograma = "cronograma.schema.json";
    private const string Homologacao = "homologacao.schema.json";
    private const string Motivo = "motivo.schema.json";

    [Theory(DisplayName = "O identificador da conta aceita texto opaco, com ou sem a forma de UUID")]
    [InlineData("0196b3c2-7d4e-7a51-9c2f-3e8b5a1d6f40")]
    [InlineData("conta_9f3a")]
    [InlineData("a b")]
    public void ContaId_TextoOpaco_Valida(string conta) =>
        CaminhosComErro(JsonNode.Parse($"\"{conta}\"")!, $"{Comuns}#/$defs/identificadorDaConta").Should().BeEmpty();

    [Theory(DisplayName = "O identificador da conta recusa o vazio, o espaço nas pontas e o que passa de 255 caracteres")]
    [InlineData("")]
    [InlineData(" conta")]
    [InlineData("conta ")]
    public void ContaId_Vazio_OuComEspacoNasPontas_Recusa(string conta) =>
        CaminhosComErro(JsonNode.Parse($"\"{conta}\"")!, $"{Comuns}#/$defs/identificadorDaConta").Should().NotBeEmpty();

    [Fact(DisplayName = "O identificador da conta com 256 caracteres é recusado e com 255 é aceito")]
    public void ContaId_LimiteDeTamanho()
    {
        CaminhosComErro(JsonNode.Parse($"\"{new string('a', 255)}\"")!, $"{Comuns}#/$defs/identificadorDaConta").Should().BeEmpty();
        CaminhosComErro(JsonNode.Parse($"\"{new string('a', 256)}\"")!, $"{Comuns}#/$defs/identificadorDaConta").Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Os assinantes da fase são contas em ordem e a conta repetida é recusada")]
    public void Assinantes_ContasEmOrdem_RepetidaRecusa()
    {
        JsonNode valida = Documento("""{ "chave": "resultado-final", "faseCanonicaCodigo": "RESULTADO_FINAL", "assinantes": ["0196b3c2-7d4e-7a51-9c2f-3e8b5a1d6f40", "conta-2"] }""");
        JsonNode repetida = Documento("""{ "chave": "resultado-final", "faseCanonicaCodigo": "RESULTADO_FINAL", "assinantes": ["conta-1", "conta-1"] }""");

        CaminhosComErro(valida, $"{Cronograma}#/$defs/fase").Should().BeEmpty();
        CaminhosComErro(repetida, $"{Cronograma}#/$defs/fase").Should().Contain("/assinantes");
    }

    [Fact(DisplayName = "Os tipos de decisão da fase e da etapa são do vocabulário e cada um aparece uma vez")]
    public void TiposDeDecisao_DoVocabulario_SemRepeticao()
    {
        JsonNode valida = Documento("""{ "chave": "f", "faseCanonicaCodigo": "INSCRICAO", "tiposDeDecisao": ["HOMOLOGACAO_DAS_INSCRICOES", "ANALISE_DOCUMENTAL"] }""");
        JsonNode repetida = Documento("""{ "chave": "f", "faseCanonicaCodigo": "INSCRICAO", "tiposDeDecisao": ["ANALISE_DOCUMENTAL", "ANALISE_DOCUMENTAL"] }""");
        JsonNode desconhecida = Documento("""{ "chave": "f", "faseCanonicaCodigo": "INSCRICAO", "tiposDeDecisao": ["ANALISE_DOCUMENTAL", "SORTEIO"] }""");

        CaminhosComErro(valida, $"{Cronograma}#/$defs/fase").Should().BeEmpty();
        CaminhosComErro(repetida, $"{Cronograma}#/$defs/fase").Should().Contain("/tiposDeDecisao");
        CaminhosComErro(desconhecida, $"{Cronograma}#/$defs/fase").Should().Contain("/tiposDeDecisao/1");
        CaminhosComErro(desconhecida, "etapas.schema.json").Should().Contain("/tiposDeDecisao/1");
    }

    [Fact(DisplayName = "O motivo na fase ou na etapa, com tipo e código do catálogo, valida; a justificativa só vem se houver texto")]
    public void Motivo_NoLugar_Valida()
    {
        JsonNode naFase = Documento("""
            { "faseChave": "homologacao", "tipoCodigo": "ISENCAO_DA_TAXA", "codigo": "RENDA_ACIMA_DO_LIMITE", "descricao": "Renda acima do limite",
              "sentido": "DESFAVORAVEL", "justificativaDaInclusao": "Motivo previsto no edital retificado", "versaoDoEditalDeEntrada": 2 }
            """);
        JsonNode naEtapa = Documento("""{ "etapaChave": "prova-objetiva", "tipoCodigo": "CLASSIFICACAO", "codigo": "NOTA_ZERADA" }""");

        CaminhosComErro(naFase, Motivo).Should().BeEmpty();
        CaminhosComErro(naEtapa, Motivo).Should().BeEmpty();
    }

    [Fact(DisplayName = "O motivo sem lugar ou com fase e etapa juntas é recusado, e a chave e a versão de entrada seguem o formato")]
    public void Motivo_ComLugarInvalido_Recusa()
    {
        JsonNode semLugar = Documento("""{ "tipoCodigo": "ISENCAO_DA_TAXA", "codigo": "RENDA" }""");
        JsonNode comOsDois = Documento("""{ "faseChave": "homologacao", "etapaChave": "prova", "tipoCodigo": "ISENCAO_DA_TAXA", "codigo": "RENDA" }""");
        JsonNode foraDoFormato = Documento("""{ "faseChave": "Homologação", "tipoCodigo": "ISENCAO_DA_TAXA", "codigo": "RENDA", "versaoDoEditalDeEntrada": 0 }""");

        CaminhosComErro(semLugar, Motivo).Should().Contain("");
        CaminhosComErro(comOsDois, Motivo).Should().Contain("");
        CaminhosComErro(foraDoFormato, Motivo).Should().Contain(["/faseChave", "/versaoDoEditalDeEntrada"]);
    }

    [Fact(DisplayName = "O motivo sem tipo, com código fora do formato, tipo desconhecido e sentido inválido é recusado com o caminho")]
    public void Motivo_ForaDoFormato_Recusa()
    {
        JsonNode motivo = Documento("""{ "faseChave": "homologacao", "tipoCodigo": "SORTEIO", "codigo": "renda", "sentido": "NEUTRO", "justificativaDaInclusao": "" }""");

        CaminhosComErro(motivo, Motivo).Should().Contain(["/tipoCodigo", "/codigo", "/sentido", "/justificativaDaInclusao"]);
        CaminhosComErro(Documento("""{ "faseChave": "homologacao", "codigo": "RENDA" }"""), Motivo).Should().Contain("");
    }

    [Fact(DisplayName = "A regra de homologação por alternativas e condições valida e a sem alternativa homologa toda inscrição enviada")]
    public void Homologacao_PorAlternativas_Valida()
    {
        JsonNode comAlternativas = Documento("""
            { "resultadoAtoCodigo": "RESULTADO_INSCRICOES_HOMOLOGADAS",
              "alternativas": [ { "condicoes": [
                { "tipo": "RESULTADO_NO_RETORNO_DO_INEP", "edicaoDoExame": 2026, "motivoDesfavoravelCodigo": "SEM_RESULTADO_NO_INEP",
                  "enquadramento": [ [ { "fato": "CONCLUIU_ENSINO_MEDIO_EM", "operador": "IGUAL", "valor": 2026 } ] ] },
                { "tipo": "RESULTADO_NO_RETORNO_DO_INEP", "edicaoDoExame": 2025, "motivoDesfavoravelCodigo": "SEM_RESULTADO_NO_INEP", "enquadramento": null } ] } ] }
            """);

        CaminhosComErro(comAlternativas, Homologacao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "resultadoAtoCodigo": "RESULTADO_FINAL", "alternativas": [] }"""), Homologacao).Should().BeEmpty();
    }

    [Fact(DisplayName = "A alternativa sem condição, a edição fora do intervalo e o tipo desconhecido são recusados com o caminho")]
    public void Homologacao_ForaDaRegra_Recusa()
    {
        JsonNode regra = Documento("""
            { "resultadoAtoCodigo": "resultado", "alternativas": [
              { "condicoes": [ { "tipo": "NOTA_MINIMA", "edicaoDoExame": 1997, "motivoDesfavoravelCodigo": "sem" } ] } ] }
            """);

        CaminhosComErro(regra, Homologacao).Should().Contain(
            ["/resultadoAtoCodigo", "/alternativas/0/condicoes/0/tipo", "/alternativas/0/condicoes/0/edicaoDoExame", "/alternativas/0/condicoes/0/motivoDesfavoravelCodigo"]);
        CaminhosComErro(Documento("""{ "alternativas": [ { "condicoes": [] } ] }"""), Homologacao).Should().Contain("/alternativas/0/condicoes");
    }

    [Fact(DisplayName = "O documento com os motivos do processo e a regra de homologação na fase valida")]
    public void Documento_ComMotivosERegraNaFase_Valida()
    {
        JsonNode documento = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "cronograma": { "fases": [ { "chave": "homologacao", "faseCanonicaCodigo": "HOMOLOGACAO_INSCRICOES", "tiposDeDecisao": ["HOMOLOGACAO_DAS_INSCRICOES"],
                "regraDeHomologacao": { "resultadoAtoCodigo": "RESULTADO_INSCRICOES_HOMOLOGADAS", "alternativas": [] }, "assinantes": ["conta-1"] } ] },
              "motivos": [ { "faseChave": "homologacao", "tipoCodigo": "HOMOLOGACAO_DAS_INSCRICOES", "codigo": "SEM_RESULTADO_NO_INEP" } ] }
            """);

        CaminhosComErro(documento).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "motivos": [ { "codigo": "RENDA" } ] }""")).Should().Contain("/motivos/0");
    }
}
