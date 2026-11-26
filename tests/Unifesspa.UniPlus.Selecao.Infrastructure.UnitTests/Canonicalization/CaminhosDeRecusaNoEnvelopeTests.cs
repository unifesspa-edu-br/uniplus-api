namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class CaminhosDeRecusaNoEnvelopeTests
{
    [Fact]
    public void LerFatosColetados_TipoDeCampoDesconhecido_RecusaComCaminhoDoDocumento()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        FatoColetado fato = ItensDoFormularioDeTeste.Campo("FATO", 0, formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaInscricao)!);
        JsonArray fatos = SerializacaoDosFatos.SerializarFatosColetados([fato], formularios.CodigosPorIdentificador(), null);
        fatos[0]!["tipoDeCampo"] = "DESCONHECIDO";
        LeitorEnvelope leitor = new();

        LeituraDosFatos.LerFatosColetados(leitor, new JsonObject { ["dadosDosFormularios"] = fatos }, formularios);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("dadosDosFormularios[0].tipoDeCampo");
    }

    [Fact]
    public void LerCalendarioDiasUteis_VersaoForaDaFormaCanonica_RecusaComCaminhoDoDocumento()
    {
        CalendarioDiasUteisCongelado calendario = CalendarioDiasUteisCongelado.Criar(Guid.NewGuid(), "2026", [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value!;
        JsonObject bloco = JsonNode.Parse(SerializacaoDoEdital.SerializarCalendarioDiasUteis(calendario).ToJsonString())!.AsObject();
        bloco["versaoDoCalendario"] = " 2026 ";
        LeitorEnvelope leitor = new();

        LeituraDoCalendario.LerCalendarioDiasUteis(leitor, new JsonObject { ["calendarioDiasUteis"] = bloco }, [], []);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("calendarioDiasUteis.versaoDoCalendario");
    }
}
