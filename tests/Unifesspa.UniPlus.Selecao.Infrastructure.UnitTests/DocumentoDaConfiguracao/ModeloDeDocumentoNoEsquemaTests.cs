namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class ModeloDeDocumentoNoEsquemaTests
{
    private static readonly LeituraDoDocumentoDaConfiguracao Leitura = new(new EsquemaDoDocumentoDaConfiguracao());

    [Fact]
    public void Ler_ModeloValido_ConservaIdentificadorERetiraDescricao()
    {
        Result<DocumentoLido> resultado = Ler("0198a000-0000-7000-8000-000000000001");

        resultado.IsSuccess.Should().BeTrue();
        JsonNode exigencia = resultado.Value!.Documento["documentosExigidos"]!["exigencias"]![0]!;
        exigencia["modeloId"]!.GetValue<string>().Should().Be("0198a000-0000-7000-8000-000000000001");
        exigencia["modelo"].Should().BeNull();
    }

    [Fact]
    public void Ler_ModeloComTextoQueNaoEIdentificador_RecusaNoCampo()
    {
        Result<DocumentoLido> resultado = Ler("modelo-invalido");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("/documentosExigidos/exigencias/0/modeloId");
    }

    private static Result<DocumentoLido> Ler(string identificador) => Leitura.Ler($$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "documentosExigidos": { "exigencias": [ { "chave": "declaracao", "obrigatorio": true, "abrePrazoParaReenvio": false, "modeloId": "{{identificador}}",
            "modelo": { "nomeArquivo": "Declaração.odt", "formato": "ODT" } } ] } }
        """);
}
