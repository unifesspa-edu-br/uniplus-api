namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CaminhosDeRecusaNoEnvelopeTests
{
    [Fact]
    public void LerFatosColetados_TipoDeCampoDesconhecido_RecusaComCaminhoDoDocumento()
    {
        JsonArray fatos = SerializacaoDosFatos.SerializarFatosColetados([FatoColetadoDeTeste.Coletando("FATO", 0).Construir()], null);
        fatos[0]!["tipoDeCampo"] = "DESCONHECIDO";
        LeitorEnvelope leitor = new();

        LeituraDosFatos.LerFatosColetados(leitor, new JsonObject { ["dadosDosFormularios"] = fatos });

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("dadosDosFormularios[0].tipoDeCampo").And.NotContain("tipoRenderizacao");
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
        leitor.Erro.Message.Should().Contain("calendarioDiasUteis.versaoDoCalendario").And.NotContain("versaoDataset");
    }
}
