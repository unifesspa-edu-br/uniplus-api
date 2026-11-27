namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoCongelado;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class QuadroDePesosDoRascunhoIgualAoPublicadoTests
{
    private const string Resolucao = "Res. 805/2024";

    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    [Fact(DisplayName = "O quadro de pesos do rascunho é igual ao da versão publicada para a mesma resolução")]
    public async Task Quadro_MesmaResolucao_RascunhoIgualAoPublicado()
    {
        ResolucaoPesoAreaEnemView cadastrada = ResolucaoDeTeste();

        JsonNode? doRascunho = (await ExportarRascunhoAsync(cadastrada))["classificacao"]!["quadroPesoAreaEnem"];
        JsonNode? doPublicado = ProjetarPublicada(cadastrada)["classificacao"]!["quadroPesoAreaEnem"];

        doRascunho.Should().NotBeNull();
        JsonNode.DeepEquals(doRascunho, doPublicado).Should().BeTrue("o rascunho e a versão publicada descrevem o quadro do mesmo jeito");
    }

    private static async Task<JsonObject> ExportarRascunhoAsync(ResolucaoPesoAreaEnemView cadastrada)
    {
        JsonObject documento = JsonNode.Parse($$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "classificacao": { "baseadoEmEnem": true, "resolucaoPesoAreaEnem": "{{Resolucao}}" } }""")!.AsObject();
        await new DescricaoDoDocumentoDoRascunho(Esquema, [new DescricaoDePesosPorAreaDoEnem(new LeitorDePesosDeTeste(cadastrada), Esquema)])
            .DescreverAsync(documento, CancellationToken.None);
        return documento;
    }

    private static JsonObject ProjetarPublicada(ResolucaoPesoAreaEnemView cadastrada)
    {
        JsonObject congelado = new()
        {
            ["classificacao"] = new JsonObject { ["quadroPesoAreaEnem"] = AreasDoEnemNoDocumentoCongelado.QuadroDePesos(Congelar(cadastrada)) },
        };
        return new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado.ToJsonString()));
    }

    private static List<GrupoPesoAreaEnemCongelado> Congelar(ResolucaoPesoAreaEnemView cadastrada) =>
        [.. cadastrada.Linhas.Select(static linha => GrupoPesoAreaEnemCongelado.Criar(
            linha.GrupoCurso.Codigo, linha.GrupoCurso.Rotulo, linha.BaseLegal,
            linha.Areas.Select(static area => ((string?)area.Codigo, (string?)area.Rotulo, area.Peso, area.Corte))).Value!)];

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
