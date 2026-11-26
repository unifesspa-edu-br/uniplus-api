namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class ConsequenciaDaAusenciaNaExportacaoTests
{
    [Theory]
    [InlineData("cronograma", "fases", "bancasRequeridas")]
    [InlineData("etapas", null, "bancas")]
    public void Projetar_BancaPublicada_UsaCodigoERotuloCongelados(string parte, string? fases, string bancas)
    {
        string banca = """{"chave":"banca","codigo":"BANCA","consequenciaDaAusencia":"ELIMINA_DO_PROCESSO","contexto":{"consequenciaDaAusenciaRotulo":"Rótulo congelado"}}""";
        string lista = $"[{{\"chave\":\"parte\",\"{bancas}\":[{banca}]}}]";
        string congelado = fases is null ? $"{{\"etapas\":{lista}}}" : $"{{\"cronogramaFases\":{{\"fases\":{lista}}}}}";
        JsonObject documento = new ProjecaoDaVersaoPublicada(new EsquemaDoDocumentoDaConfiguracao()).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        JsonNode bancaProjetada = fases is null ? documento[parte]![0]![bancas]![0]! : documento[parte]![fases]![0]![bancas]![0]!;
        bancaProjetada["consequenciaDaAusencia"]!.GetValue<string>().Should().Be("ELIMINA_DO_PROCESSO");
        bancaProjetada["consequenciaDaAusenciaRotulo"]!.GetValue<string>().Should().Be("Rótulo congelado");
    }

    [Fact]
    public void Projetar_FormularioPublicado_UsaCodigoERotuloCongelados()
    {
        string formulario = """{"codigo":"HABILITACAO","consequenciaDaFaltaDeEnvio":"RETIRA_O_ACRESCIMO","contexto":{"consequenciaDaFaltaDeEnvioRotulo":"Rótulo congelado"}}""";
        string congelado = $"{{\"formularios\":[{formulario}]}}";

        JsonObject documento = new ProjecaoDaVersaoPublicada(new EsquemaDoDocumentoDaConfiguracao()).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));

        JsonNode projetado = documento["formularios"]![0]!;
        projetado["consequenciaDaFaltaDeEnvio"]!.GetValue<string>().Should().Be("RETIRA_O_ACRESCIMO");
        projetado["consequenciaDaFaltaDeEnvioRotulo"]!.GetValue<string>().Should().Be("Rótulo congelado");
    }
}
