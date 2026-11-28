namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// A importação do resultado do documento é copiada como foi escrita e passa pela leitura do domínio: cada recusa de forma aponta o
/// campo da importação, e a primeira fase é ligada à fase do cronograma do mesmo documento pela chave.
/// </summary>
public sealed class LeituraDaImportacaoDoResultadoDoDocumentoTests
{
    private const string Cronograma = """ "cronograma": { "fases": [ { "chave": "fase_resultado" }, { "chave": "fase_matricula" } ] } """;

    private static DeclaracoesDoDocumento Declaracoes(string importacao) =>
        DeclaracoesDoDocumentoLidas.De(JsonNode.Parse($$"""{ {{Cronograma}}, "importacao": {{importacao}} }""")!.AsObject());

    private static ImportacaoDoResultadoLida Lida(string importacao, bool inscricaoPeloSistema = false)
    {
        DeclaracoesDoDocumento declaracoes = Declaracoes(importacao);
        return ImportacaoDoResultadoLida.Ler(declaracoes.Importacao, declaracoes.Fases, inscricaoPeloSistema);
    }

    private static string[] Campos(ImportacaoDoResultadoLida lida) =>
        [.. lida.Origem.PartesRecusadas.Select(static parte => parte.Causa.Field!)];

    [Fact(DisplayName = "O documento sem o bloco da importação fica sem bloco e por declarar")]
    public void BlocoAusente_FicaSemBlocoEPorDeclarar()
    {
        DeclaracoesDoDocumentoLidas.De(JsonNode.Parse("{}")!.AsObject()).Importacao.Should().BeNull();
        ImportacaoDoResultadoLida.Ler(null, [], inscricaoPeloSistema: false).Declaracao.Should().BeNull();
    }

    [Fact(DisplayName = "O bloco sem relatório e sem primeira fase é o processo por declarar, sem recusa")]
    public void BlocoVazio_PorDeclararSemRecusa()
    {
        ImportacaoDoResultadoLida lida = Lida("""{ "relatorios": [] }""");

        lida.Declaracao.Should().BeNull();
        lida.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O bloco copia os relatórios na ordem escrita e liga a primeira fase pela chave")]
    public void Bloco_CopiaRelatoriosELigaAFase()
    {
        string bloco = """
            { "relatorios": [ { "leiauteCodigo": "INEP_RETORNO_ENEM", "leiauteVersao": "v1" } ], "primeiraFaseDepoisDaImportacaoChave": "fase_matricula" }
            """;
        DeclaracoesDoDocumento declaracoes = Declaracoes(bloco);

        declaracoes.Importacao!.PrimeiraFaseDepoisDaImportacaoChave.Should().Be("fase_matricula");
        declaracoes.Importacao.Relatorios.Should().Equal(new RelatorioInformado("INEP_RETORNO_ENEM", "v1"));
        ImportacaoDoResultadoLida lida = ImportacaoDoResultadoLida.Ler(declaracoes.Importacao, declaracoes.Fases, inscricaoPeloSistema: false);
        lida.Declaracao!.PrimeiraFaseDepoisDaImportacaoId.Should().NotBeNull();
        lida.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que o cronograma do documento não traz é recusada no campo da primeira fase")]
    public void FaseForaDoCronograma_ApontaOCampoDaFase()
    {
        ImportacaoDoResultadoLida lida = Lida("""
            { "relatorios": [ { "leiauteCodigo": "INEP_RETORNO_ENEM", "leiauteVersao": "v1" } ], "primeiraFaseDepoisDaImportacaoChave": "fase_inexistente" }
            """);

        lida.Declaracao.Should().BeNull();
        lida.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Code).Should().Equal(ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma);
        Campos(lida).Should().Equal("/importacao/primeiraFaseDepoisDaImportacaoChave");
    }

    [Fact(DisplayName = "O leiaute repetido e o relatório sem versão são recusados com o código do domínio")]
    public void FormaInvalida_RecusaComOCodigoDoDominio()
    {
        ImportacaoDoResultadoLida repetido = Lida("""
            { "relatorios": [ { "leiauteCodigo": "A", "leiauteVersao": "v1" }, { "leiauteCodigo": "A", "leiauteVersao": "v2" } ] }
            """);
        ImportacaoDoResultadoLida semVersao = Lida("""{ "relatorios": [ { "leiauteCodigo": "A" } ] }""");

        repetido.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Code).Should().Contain(ImportacaoDoResultadoErrorCodes.RelatorioRepetido);
        semVersao.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Code).Should().Contain(ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute);
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema que declara importação recebe só a recusa de origem, mesmo com relatório inválido")]
    public void InscricaoPeloSistema_RecebeSoARecusaDeOrigem()
    {
        ImportacaoDoResultadoLida lida = Lida("""{ "relatorios": [ { "leiauteCodigo": "MEU LEIAUTE" } ], "primeiraFaseDepoisDaImportacaoChave": "fase_inexistente" }""", inscricaoPeloSistema: true);

        lida.Declaracao.Should().BeNull();
        lida.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Code).Should().Equal(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema);
        Campos(lida).Should().Equal("/importacao");
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema sem declaração de importação não tem recusa")]
    public void InscricaoPeloSistema_SemDeclaracao_SemRecusa() =>
        Lida("""{ "relatorios": [] }""", inscricaoPeloSistema: true).Origem.PartesRecusadas.Should().BeEmpty();

    [Theory(DisplayName = "O código do leiaute fora da forma de código de catálogo é recusado no campo do código")]
    [InlineData("MEU LEIAUTE")]
    [InlineData("LEIAUTE\u0301")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef")]
    public void CodigoForaDaForma_RecusaNoCampoDoCodigo(string codigo)
    {
        ImportacaoDoResultadoLida lida = Lida($$"""{ "relatorios": [ { "leiauteCodigo": "{{codigo}}", "leiauteVersao": "v1" } ] }""");

        lida.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Code).Should().Equal(ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute);
    }
}
