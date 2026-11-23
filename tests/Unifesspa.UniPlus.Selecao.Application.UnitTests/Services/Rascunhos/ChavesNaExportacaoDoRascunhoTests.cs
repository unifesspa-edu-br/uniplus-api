namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class ChavesNaExportacaoDoRascunhoTests
{
    [Fact]
    public async Task Exportar_RascunhoComFaseEEtapa_PreservaAsChaves()
    {
        const string documento = """
            { "identificacao": { "nome": "Processo de exemplo" },
              "cronograma": { "fases": [{ "chave": "fase_inscricao" }] },
              "etapas": [{ "chave": "etapa_prova" }] }
            """;
        CenarioDoRascunho cenario = new();
        RascunhoDeProcessoSeletivo rascunho = cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, documento);

        JsonObject exportado = await cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        exportado["cronograma"]!["fases"]![0]!["chave"]!.GetValue<string>().Should().Be("fase_inscricao");
        exportado["etapas"]![0]!["chave"]!.GetValue<string>().Should().Be("etapa_prova");
    }
}
