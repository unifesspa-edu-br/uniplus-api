namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

/// <summary>
/// O documento divulgado do certame com prazo de emissão e validade: a forma que sai ao candidato, sem campo interno da
/// exigência, a volta do documento gravado e a recusa da leitura quando o envelope congelado não tem a forma esperada.
/// </summary>
public sealed class PrazoEValidadeNoDocumentoDoCertameTests
{
    private static readonly string[] CamposDaExigencia = ["rotulo", "aplicabilidade", "obrigatorio", "formatos", "prazoDeEmissao", "validade", "modelo"];

    private static JsonObject Exigencia(JsonObject envelope) => envelope["documentosExigidos"]!["exigencias"]![0]!.AsObject();

    private static EnvelopeDoCertameBuilder ComPrazoEValidade() => new EnvelopeDoCertameBuilder()
        .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Construir())
        .ComValidade(new ValidadeDoDocumentoBuilder().Construir());

    private static JsonObject ExigenciaSerializada(JsonObject envelope)
    {
        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado(envelope);
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        return JsonNode.Parse(documento)!["documentosExigidos"]![0]!.AsObject();
    }

    private static IEnumerable<string> Campos(JsonNode? objeto) => objeto!.AsObject().Select(static campo => campo.Key);

    private static void ConferirRecusa(JsonObject envelope)
    {
        Result<CertamePublicadoDto> resultado = ProjecaoDoCertameDeTeste.Projetar(envelope);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ProjecaoDoCertamePublicado.CodigoEnvelopeInesperado);
        resultado.Error.Message.Should().Contain("'documentos exigidos'");
    }

    [Fact(DisplayName = "A exigência divulgada traz só os campos do contrato do candidato, e o prazo e a validade, só os seus")]
    public void Documento_Exigencia_NaoExpoeCampoInterno()
    {
        JsonObject exigencia = ExigenciaSerializada(ComPrazoEValidade().Construir());

        Campos(exigencia).Should().Equal(CamposDaExigencia);
        Campos(exigencia["prazoDeEmissao"]).Should().Equal("escolha", "valor", "unidade", "referencia", "efeito");
        Campos(exigencia["prazoDeEmissao"]!["referencia"]).Should().Equal("tipo", "data", "faseId");
        Campos(exigencia["validade"]).Should().Equal("escolha", "referencia", "efeito");
        Campos(exigencia["validade"]!["efeito"]).Should().Equal("codigo", "rotulo");
    }

    [Fact(DisplayName = "O prazo e a validade saem pelo código e pelo rótulo, com a data no formato de data")]
    public void Documento_PrazoEValidade_SaemComCodigoRotuloEData()
    {
        JsonObject exigencia = ExigenciaSerializada(ComPrazoEValidade().Construir());

        exigencia["prazoDeEmissao"]!["escolha"]!["codigo"]!.GetValue<string>().Should().Be("COM_PRAZO");
        exigencia["prazoDeEmissao"]!["unidade"]!["codigo"]!.GetValue<string>().Should().Be("DIAS");
        exigencia["prazoDeEmissao"]!["referencia"]!["data"]!.GetValue<string>().Should().Be("2026-01-30");
        exigencia["validade"]!["efeito"]!["codigo"]!.GetValue<string>().Should().Be("RECUSAR_DOCUMENTO");
    }

    [Fact(DisplayName = "O documento gravado volta igual ao que foi projetado")]
    public void Documento_PrazoEValidade_VoltaIgualDoDocumentoGravado()
    {
        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado(ComPrazoEValidade().Construir());
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);

        CertamePublicadoDto lido = JsonSerializer.Deserialize<CertamePublicadoDto>(documento, ProjecaoDoCertamePublicado.OpcoesDoDocumento)!;

        lido.DocumentosExigidos.Should().BeEquivalentTo(certame.DocumentosExigidos);
    }

    [Fact(DisplayName = "A exigência sem a chave do prazo de emissão recusa a leitura do certame")]
    public void Projetar_ChaveDoPrazoAusente_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope).Remove("prazoDeEmissao");

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "A exigência sem a chave da validade recusa a leitura do certame")]
    public void Projetar_ChaveDaValidadeAusente_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope).Remove("validade");

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "A unidade de prazo que o catálogo não conhece recusa a leitura do certame")]
    public void Projetar_UnidadeDesconhecida_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope)["prazoDeEmissao"]!["unidade"] = "decadas";

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "O prazo com prazo e sem valor recusa a leitura do certame")]
    public void Projetar_PrazoSemValor_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope)["prazoDeEmissao"]!["valor"] = null;

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "A data de referência fora do formato de data recusa a leitura do certame")]
    public void Projetar_DataForaDoFormato_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope)["prazoDeEmissao"]!["referencia"] = JsonNode.Parse("""{"tipo":"DATA_ESPECIFICA","data":"31/01/2026","faseId":null}""");

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "A referência a fase sem a fase recusa a leitura do certame")]
    public void Projetar_ReferenciaDeFaseSemFase_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope)["validade"]!["referencia"] = JsonNode.Parse("""{"tipo":"FIM_DA_FASE","data":null,"faseId":null}""");

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "A validade exigida sem referência recusa a leitura do certame")]
    public void Projetar_ValidadeExigidaSemReferencia_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        Exigencia(envelope)["validade"]!["referencia"] = null;

        ConferirRecusa(envelope);
    }

    [Fact(DisplayName = "A fase do cronograma sem identificador recusa a leitura das datas de referência")]
    public void Projetar_FaseSemIdentificador_Recusa()
    {
        JsonObject envelope = ComPrazoEValidade().Construir();
        envelope["cronogramaFases"]!["fases"]![0]!.AsObject().Remove("id");

        ConferirRecusa(envelope);
    }
}
