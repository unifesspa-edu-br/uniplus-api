namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class ChavesNaExportacaoDoRascunhoTests
{
    private const string Documento = """
            { "identificacao": { "nome": "Processo de exemplo" },
              "cronograma": { "fases": [{ "chave": "fase_inscricao", "bancasRequeridas": [{ "chave": "banca_fase" }] }] },
              "etapas": [{ "chave": "etapa_prova", "bancas": [{ "chave": "banca_etapa" }], "itensDeAvaliacao": [{ "chave": "item_redacao" }],
                "escalaDeAtendimento": [{ "chave": "nivel_atende" }] }] }
            """;

    [Fact]
    public async Task Exportar_RascunhoComFaseEEtapa_PreservaAsChaves()
    {
        CenarioDoRascunho cenario = new();
        RascunhoDeProcessoSeletivo rascunho = cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, Documento);

        JsonObject exportado = await cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        exportado["cronograma"]!["fases"]![0]!["bancasRequeridas"]![0]!["chave"]!.GetValue<string>().Should().Be("banca_fase");
        exportado["etapas"]![0]!["bancas"]![0]!["chave"]!.GetValue<string>().Should().Be("banca_etapa");
        exportado["cronograma"]!["fases"]![0]!["chave"]!.GetValue<string>().Should().Be("fase_inscricao");
        exportado["etapas"]![0]!["chave"]!.GetValue<string>().Should().Be("etapa_prova");
        exportado["etapas"]![0]!["itensDeAvaliacao"]![0]!["chave"]!.GetValue<string>().Should().Be("item_redacao");
        exportado["etapas"]![0]!["escalaDeAtendimento"]![0]!["chave"]!.GetValue<string>().Should().Be("nivel_atende");
    }
}
