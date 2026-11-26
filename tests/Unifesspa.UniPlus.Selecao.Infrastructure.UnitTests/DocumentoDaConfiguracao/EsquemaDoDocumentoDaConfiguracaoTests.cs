namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class EsquemaDoDocumentoDaConfiguracaoTests
{
    private const string Cabecalho = "\"formato\": \"uniplus.selecao.configuracao\", \"versaoFormato\": 1";

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    private static IReadOnlyList<FieldError> Avaliar(string json) => Esquema.Avaliar(JsonNode.Parse(json)!.AsObject());

    private static IReadOnlyList<FieldError> AvaliarComCabecalho(string resto) => Avaliar($"{{ {Cabecalho}, {resto} }}");

    private static (string? Campo, string Codigo) Resumo(FieldError causa) => (causa.Field, causa.Error.Code);

    [Theory(DisplayName = "Os exemplos publicados validam contra o esquema embutido")]
    [MemberData(nameof(ExemplosDoContrato.Arquivos), MemberType = typeof(ExemplosDoContrato))]
    public void Exemplo_ValidaContraOEsquemaEmbutido(string arquivo) =>
        Avaliar(ExemplosDoContrato.Ler(arquivo)).Should().BeEmpty();

    [Fact(DisplayName = "O documento incompleto, só com formato e versão do formato, é válido")]
    public void DocumentoIncompleto_EValido() => Avaliar($"{{ {Cabecalho} }}").Should().BeEmpty();

    [Fact(DisplayName = "A chamada do cadastro de reserva sem número é válida pela forma, porque a conferência do número é do domínio")]
    public void ChamadaDoCadastroDeReservaSemNumero_EValidaPelaForma() =>
        AvaliarComCabecalho("""
            "cadastroDeReserva": [ { "pessoasDaReservaPorVaga": 0 }, { } ]
            """).Should().BeEmpty();

    [Fact(DisplayName = "O número da chamada com tipo errado continua recusado no ponteiro do número")]
    public void NumeroDaChamadaComTipoErrado_ApontaONumero() =>
        AvaliarComCabecalho("""
            "cadastroDeReserva": [ { "numeroDaChamada": "primeira" } ]
            """).Select(Resumo).Should().Contain(("/cadastroDeReserva/0/numeroDaChamada", DocumentoDaConfiguracaoErrorCodes.TipoInvalido));

    [Fact(DisplayName = "O campo que o esquema não declara é recusado no ponteiro do próprio campo")]
    public void CampoDesconhecido_ApontaOProprioCampo() =>
        AvaliarComCabecalho("""
            "invento": 1, "etapas": [ { "chave": "etapa_1", "outroInvento": true } ]
            """).Select(Resumo).Should().BeEquivalentTo(
        [
            ("/invento", DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido),
            ("/etapas/0/outroInvento", DocumentoDaConfiguracaoErrorCodes.CampoDesconhecido),
        ]);

    [Fact(DisplayName = "O tipo errado é recusado dizendo o tipo esperado e sem repetir o valor")]
    public void TipoErrado_DizOTipoEsperadoSemRepetirOValor()
    {
        IReadOnlyList<FieldError> causas = AvaliarComCabecalho("""
            "etapas": "SEGREDO-123", "opcoesDeCurso": { "numero": "SEGREDO-456" }
            """);

        causas.Select(Resumo).Should().Contain(("/etapas", DocumentoDaConfiguracaoErrorCodes.TipoInvalido));
        causas.Single(causa => causa.Field == "/etapas").Error.Message.Should().Contain("lista");
        causas.Select(causa => causa.Error.Message).Should().NotContain(mensagem => mensagem.Contains("SEGREDO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O valor fora da enumeração é recusado como valor inválido, sem repetir o valor")]
    public void ValorForaDaEnumeracao_EValorInvalido()
    {
        IReadOnlyList<FieldError> causas = AvaliarComCabecalho("""
            "opcoesDeCurso": { "numero": 5 }
            """);

        causas.Should().ContainSingle().Which.Field.Should().Be("/opcoesDeCurso/numero");
        causas[0].Error.Code.Should().Be(DocumentoDaConfiguracaoErrorCodes.ValorInvalido);
        causas[0].Error.Message.Should().NotContain("5");
    }

    [Theory]
    [InlineData("cronograma", "fases", "bancasRequeridas", "/cronograma/fases/0/bancasRequeridas/0/consequenciaDaAusencia")]
    [InlineData("etapas", null, "bancas", "/etapas/0/bancas/0/consequenciaDaAusencia")]
    public void ConsequenciaDaAusencia_ValorDesconhecido_RecusadoNoCaminho(string parte, string? fases, string bancas, string caminho)
    {
        string lista = $"[{{\"chave\":\"parte\",\"{bancas}\":[{{\"chave\":\"banca\",\"tipoBancaCodigo\":\"BANCA\",\"consequenciaDaAusencia\":\"DESCONHECIDA\"}}]}}]";
        string valor = fases is null ? lista : $"{{\"{fases}\":{lista}}}";
        AvaliarComCabecalho($"\"{parte}\":{valor}").Select(Resumo).Should().Contain((caminho, DocumentoDaConfiguracaoErrorCodes.ValorInvalido));
    }

    [Fact(DisplayName = "A consequência da falta de envio desconhecida é recusada no campo do formulário")]
    public void ConsequenciaDaFaltaDeEnvio_ValorDesconhecido_RecusadoNoCaminho() =>
        AvaliarComCabecalho("""
            "formularios": [ { "finalidade": "HABILITACAO", "paginas": [], "consequenciaDaFaltaDeEnvio": "DESCONHECIDA" } ]
            """).Select(Resumo).Should().Contain(("/formularios/0/consequenciaDaFaltaDeEnvio", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "A chave fora do padrão é recusada como valor inválido")]
    public void ChaveForaDoPadrao_EValorInvalido() =>
        AvaliarComCabecalho("""
            "etapas": [ { "chave": "Etapa Um" } ]
            """).Select(Resumo).Should().Equal(("/etapas/0/chave", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "O campo obrigatório ausente é recusado no ponteiro do campo ausente")]
    public void CampoObrigatorioAusente_ApontaOCampoAusente() =>
        Avaliar("""{ "versaoFormato": 1, "etapas": [ { "nome": "Prova" } ] }""").Select(Resumo).Should().BeEquivalentTo(
        [
            ("/formato", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente),
            ("/etapas/0/chave", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente),
        ]);

    [Theory(DisplayName = "O formato ou a versão do formato que o sistema não suporta é recusado como versão não suportada")]
    [InlineData("""{ "formato": "outro.formato", "versaoFormato": 1 }""", "/formato")]
    [InlineData("""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 2 }""", "/versaoFormato")]
    [InlineData("""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": "1" }""", "/versaoFormato")]
    public void FormatoOuVersaoDiferente_EVersaoNaoSuportada(string json, string campo) =>
        Avaliar(json).Select(Resumo).Should().Equal((campo, DocumentoDaConfiguracaoErrorCodes.VersaoDoFormatoNaoSuportada));

    [Fact(DisplayName = "Duas regras violadas no mesmo ponteiro dão uma causa só")]
    public void MesmoPonteiro_UmaCausaSo() =>
        AvaliarComCabecalho("""
            "etapas": [ { "chave": "" } ]
            """).Select(Resumo).Should().Equal(("/etapas/0/chave", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "O que falha na condição que escolhe o ramo não é causa do documento")]
    public void CondicaoQueNaoVale_NaoGeraCausa() =>
        AvaliarComCabecalho("""
            "fada": { "aplica": false, "fator": 1.2 }
            """).Select(Resumo).Should().Equal(("/fada/fator", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));

    [Fact(DisplayName = "As avaliações simultâneas dão o mesmo resultado")]
    public async Task AvaliacoesSimultaneas_DaoOMesmoResultado()
    {
        string json = ExemplosDoContrato.Ler("inscricao-pelo-sistema.json").Replace("\"versaoFormato\": 1", "\"versaoFormato\": 3", StringComparison.Ordinal);

        (string?, string)[][] resultados = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => Avaliar(json).Select(Resumo).ToArray())));

        resultados.Should().OnlyContain(resultado => resultado.Length == 1 && resultado[0].Item1 == "/versaoFormato");
    }

    [Fact(DisplayName = "Os arquivos do esquema viajam embutidos no assembly, um por arquivo do contrato")]
    public void EsquemaEmbutido_TemUmRecursoPorArquivoDoContrato()
    {
        string pasta = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1");
        string[] arquivos = [.. Directory.GetFiles(pasta, "*.schema.json").Select(static caminho => Path.GetFileName(caminho))];

        string[] embutidos = [.. typeof(EsquemaDoDocumentoDaConfiguracao).Assembly.GetManifestResourceNames()
            .Where(static nome => nome.StartsWith("Unifesspa.UniPlus.Selecao.Contratos.Configuracao.v1.", StringComparison.Ordinal))
            .Select(static nome => nome["Unifesspa.UniPlus.Selecao.Contratos.Configuracao.v1.".Length..])];

        embutidos.Should().BeEquivalentTo(arquivos).And.NotBeEmpty();
    }

    [Fact(DisplayName = "O identificador que o contrato REST referencia é o $id do arquivo do esquema")]
    public void IdentificadorPublicado_EOIdDoArquivoDoEsquema()
    {
        string caminho = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1", "documento.schema.json");

        string? id = JsonNode.Parse(File.ReadAllText(caminho))!["$id"]?.GetValue<string>();

        EsquemaPublicadoDoDocumento.Identificador.Should().Be(id);
    }
}
