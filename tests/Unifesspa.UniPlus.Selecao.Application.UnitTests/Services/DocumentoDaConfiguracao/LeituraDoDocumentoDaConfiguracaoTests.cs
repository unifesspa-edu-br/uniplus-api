namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class LeituraDoDocumentoDaConfiguracaoTests
{
    private readonly IEsquemaDoDocumentoDaConfiguracao _esquema = Substitute.For<IEsquemaDoDocumentoDaConfiguracao>();

    public LeituraDoDocumentoDaConfiguracaoTests()
    {
        _esquema.Forma.Returns(FormaDeTeste.Documento());
        _esquema.Avaliar(Arg.Any<JsonObject>()).Returns([]);
    }

    private LeituraDoDocumentoDaConfiguracao Leitura => new(_esquema);

    private static FieldError Causa(string ponteiro, string codigo) => new(ponteiro, new DomainError(codigo, "Mensagem."));

    [Fact(DisplayName = "A estrutura recusada devolve as causas e não normaliza nem avalia")]
    public void EstruturaRecusada_NaoNormalizaNemAvalia()
    {
        Result<DocumentoLido> resultado = Leitura.Ler("{ \"a\": 1, \"a\": 2 ");

        resultado.IsFailure.Should().BeTrue();
        _esquema.DidNotReceiveWithAnyArgs().Avaliar(default!);
        _ = _esquema.DidNotReceive().Forma;
    }

    [Fact(DisplayName = "O documento é avaliado já normalizado: sem os campos só de leitura e sem inventar chave nas partes")]
    public void Avaliacao_RecebeODocumentoNormalizado()
    {
        JsonObject? recebido = null;
        _esquema.Avaliar(Arg.Do<JsonObject>(documento => recebido = (JsonObject)documento.DeepClone())).Returns([]);

        _ = Leitura.Ler("""{ "id": "x", "etapas": [ { "nome": "Prova" } ] }""");

        recebido!.ContainsKey("id").Should().BeFalse();
        recebido["etapas"]![0]!.AsObject().ContainsKey("chave").Should().BeFalse();
    }

    [Fact(DisplayName = "O documento válido devolve o normalizado, o nome e a unidade")]
    public void DocumentoValido_DevolveONormalizadoComNomeEUnidade()
    {
        Result<DocumentoLido> resultado = Leitura.Ler("""
            { "id": "x", "identificacao": { "nome": "Processo Seletivo de Exemplo", "unidadeAdministradoraCodigo": "CEPS", "unidadeNome": "Nome" } }
            """);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Nome.Should().Be("Processo Seletivo de Exemplo");
        resultado.Value.UnidadeAdministradoraCodigo.Should().Be("CEPS");
        resultado.Value.Documento.ContainsKey("id").Should().BeFalse();
        resultado.Value.Documento["identificacao"]!.AsObject().ContainsKey("unidadeNome").Should().BeFalse();
    }

    [Theory(DisplayName = "Sem identificação, ou com nome e unidade que não são texto, o nome e a unidade ficam nulos")]
    [InlineData("""{ }""")]
    [InlineData("""{ "identificacao": { } }""")]
    [InlineData("""{ "identificacao": { "nome": 7, "unidadeAdministradoraCodigo": null } }""")]
    [InlineData("""{ "identificacao": [ ] }""")]
    public void NomeEUnidadeAusentes_FicamNulos(string texto)
    {
        DocumentoLido lido = Leitura.Ler(texto).Value!;

        lido.Nome.Should().BeNull();
        lido.UnidadeAdministradoraCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "As causas do _esquema e as chaves repetidas saem juntas, na ordem do documento")]
    public void CausasDasEtapasIndependentes_SaemJuntasNaOrdemDoDocumento()
    {
        _esquema.Avaliar(Arg.Any<JsonObject>()).Returns([Causa("/etapas/1/nome", DocumentoDaConfiguracaoErrorCodes.TipoInvalido), Causa("/nome", DocumentoDaConfiguracaoErrorCodes.TipoInvalido)]);

        Result<DocumentoLido> resultado = Leitura.Ler("""{ "nome": 1, "etapas": [ { "chave": "a" }, { "chave": "a", "nome": 2 } ] }""");

        resultado.Errors.Select(static causa => causa.Field).Should().Equal("/nome", "/etapas/0/chave", "/etapas/1/chave", "/etapas/1/nome");
    }

    [Fact(DisplayName = "No mesmo ponteiro a causa do _esquema vence a da chave repetida")]
    public void MesmoPonteiro_VenceACausaDoEsquema()
    {
        _esquema.Avaliar(Arg.Any<JsonObject>()).Returns([Causa("/etapas/0/chave", DocumentoDaConfiguracaoErrorCodes.ValorInvalido)]);

        Result<DocumentoLido> resultado = Leitura.Ler("""{ "etapas": [ { "chave": "" }, { "chave": "" } ] }""");

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/0/chave", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/etapas/1/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida));
    }

    [Fact(DisplayName = "O texto é obrigatório")]
    public void TextoNulo_Lanca() => Assert.Throws<ArgumentNullException>(() => Leitura.Ler(null!));
}
