namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class RascunhoComQuadroDePesosNoEsquemaTests
{
    private const string Resolucao = "Res. 805/2024";

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    [Fact(DisplayName = "O rascunho exportado com o quadro de pesos continua válido contra o esquema v1")]
    public async Task Exportar_RascunhoComQuadro_ValidaContraOEsquema()
    {
        JsonObject documento = await ExportarRascunhoAsync(ResolucaoDeTeste());

        IReadOnlyList<FieldError> erros = Esquema.Avaliar(documento);

        erros.Should().BeEmpty("as violações do esquema são: {0}", string.Join("; ", erros.Select(static erro => $"{erro.Field}: {erro.Error.Message}")));
    }

    private static async Task<JsonObject> ExportarRascunhoAsync(ResolucaoPesoAreaEnemView cadastrada)
    {
        JsonObject documento = JsonNode.Parse($$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "classificacao": { "baseadoEmEnem": true, "resolucaoPesoAreaEnem": "{{Resolucao}}" } }""")!.AsObject();
        await new DescricaoDoDocumentoDoRascunho(Esquema, [new DescricaoDePesosPorAreaDoEnem(new LeitorDePesosDeTeste(cadastrada), Esquema)])
            .DescreverAsync(documento, CancellationToken.None);
        return documento;
    }

    // Grupos e áreas fora da ordem do código, pesos e cortes com menos casas que as quatro do esquema.
    private static ResolucaoPesoAreaEnemView ResolucaoDeTeste() => new(
        Resolucao,
        [
            Linha("TECNOLOGICA", "Tecnológica", 1.5m),
            Linha("HUMANISTICA_II", "Humanística II", 2.25m),
            Linha("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas", 3m),
            Linha("HUMANISTICA_I", "Humanística I", 1m),
        ],
        []);

    private static PesoAreaEnemView Linha(string grupoCodigo, string grupoRotulo, decimal pesoDeLinguagens) => new(
        Guid.CreateVersion7(),
        Resolucao,
        new GrupoAreaEnemView(grupoCodigo, grupoRotulo),
        [
            new("REDACAO", "Redação", 2m, 400.125m),
            new("MATEMATICA", "Matemática e suas Tecnologias", 1.5m, null),
            new("LINGUAGENS", "Linguagens e suas Tecnologias", pesoDeLinguagens, 350m),
        ],
        $"Resolução nº 805/2024/Consepe – Anexo I ({grupoRotulo})");

    private sealed class LeitorDePesosDeTeste(ResolucaoPesoAreaEnemView cadastrada) : IPesoAreaEnemReader
    {
        public Task<IReadOnlyList<PesoAreaEnemView>> ListarVivasAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(cadastrada.Linhas);

        public Task<PesoAreaEnemView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(cadastrada.Linhas.FirstOrDefault(linha => linha.Id == id));

        public Task<ResolucaoPesoAreaEnemView?> ObterPorResolucaoAsync(string resolucao, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolucaoPesoAreaEnemView?>(cadastrada);
    }
}
