namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CadastrosDasVagasDoRascunhoDeTeste;

public sealed class DescricaoDoQuadroDeVagasTests
{
    private readonly CenarioDoRascunho _cenario = new();

    public DescricaoDoQuadroDeVagasTests() => Cadastrar(_cenario);

    private async Task<JsonObject> DescreverAsync(JsonObject documento, FormaDoNo? forma = null)
    {
        VagasDoRascunhoComCadastrosVivos vagas = await _cenario.LeituraDasVagas.LerAsync(documento, CancellationToken.None);
        DescricaoDoQuadroDeVagas.Descrever(documento, vagas, forma ?? FormaDosDescritivosDeTeste.Documento());
        return documento;
    }

    private static JsonObject Documento(params string[] distribuicoes) => JsonNode.Parse(CadastrosDasVagasDoRascunhoDeTeste.Documento(distribuicoes))!.AsObject();

    private static JsonObject Item(JsonObject documento, int posicao) => documento["distribuicaoVagas"]![posicao]!.AsObject();

    private static IEnumerable<(string Codigo, int Quantidade)> Quadro(JsonObject item) =>
        item["quadro"]!.AsArray().Select(static linha => (linha!["modalidadeCodigo"]!.GetValue<string>(), linha["quantidade"]!.GetValue<int>()));

    [Fact(DisplayName = "O quadro por modalidade, em ordem do código, e o total do edital entram no fim da distribuição calculada")]
    public async Task Descrever_DistribuicaoCalculavel_EscreveOQuadroEOTotalNoFim()
    {
        JsonObject documento = await DescreverAsync(Documento(Distribuicao(OfertaDePedagogia)));

        JsonObject item = Item(documento, 0);
        DocumentoDescritoDeTeste.Chaves(item).TakeLast(2).Should().Equal("quadro", "totalDeVagasDoEdital");
        Quadro(item).Should().Equal((Ampla, 6), (AmplaComDeficiencia, 4));
        item["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(10);
    }

    [Fact(DisplayName = "A distribuição cuja oferta o cadastro não conhece fica sem o quadro e sem o total, e a outra recebe os dois na posição dela")]
    public async Task Descrever_PrimeiraDistribuicaoSemOfertaViva_SoASegundaRecebe()
    {
        JsonObject documento = await DescreverAsync(Documento(Distribuicao("OFERTA_REMOVIDA"), Distribuicao(OfertaDeMedicina)));

        Item(documento, 0).ContainsKey("quadro").Should().BeFalse();
        Item(documento, 0).ContainsKey("totalDeVagasDoEdital").Should().BeFalse();
        Quadro(Item(documento, 1)).Should().Equal((Ampla, 6), (AmplaComDeficiencia, 4));
    }

    [Fact(DisplayName = "A distribuição sem as vagas ofertadas não pode ser calculada: fica sem os dois campos e sem exceção")]
    public async Task Descrever_SemVagasOfertadas_FicaSemOsCampos()
    {
        JsonObject documento = Documento(Distribuicao(OfertaDePedagogia));
        Item(documento, 0).Remove("vagasOfertadasBase");

        await DescreverAsync(documento);

        DocumentoDescritoDeTeste.Chaves(Item(documento, 0)).Should().NotContain(["quadro", "totalDeVagasDoEdital"]);
    }

    [Fact(DisplayName = "A distribuição sem a regra de distribuição não pode ser calculada: fica sem os dois campos e sem exceção")]
    public async Task Descrever_SemRegraDeDistribuicao_FicaSemOsCampos()
    {
        JsonObject documento = Documento(Distribuicao(OfertaDePedagogia));
        Item(documento, 0).Remove("regraDistribuicao");

        await DescreverAsync(documento);

        DocumentoDescritoDeTeste.Chaves(Item(documento, 0)).Should().NotContain(["quadro", "totalDeVagasDoEdital"]);
    }

    [Fact(DisplayName = "O esquema que não marca o quadro nem o total como só de leitura não recebe nenhum dos dois")]
    public async Task Descrever_FormaSemMarcaDeSomenteLeitura_NaoEscreve()
    {
        JsonObject documento = await DescreverAsync(Documento(Distribuicao(OfertaDePedagogia)), FormaDosDescritivosDeTeste.Documento(somenteLeitura: false));

        DocumentoDescritoDeTeste.Chaves(Item(documento, 0)).Should().NotContain(["quadro", "totalDeVagasDoEdital"]);
    }

    [Fact(DisplayName = "O quadro e o total que o documento já trazia são trocados pelos calculados agora, e o da distribuição não calculada ficam como estão")]
    public async Task Descrever_CamposJaPresentes_SaoTrocadosSoNaDistribuicaoCalculada()
    {
        JsonObject calculada = JsonNode.Parse(Distribuicao(OfertaDePedagogia))!.AsObject();
        JsonObject recusada = JsonNode.Parse(Distribuicao("OFERTA_REMOVIDA"))!.AsObject();
        foreach (JsonObject item in new[] { calculada, recusada })
        {
            item["totalDeVagasDoEdital"] = 999;
        }

        JsonObject documento = await DescreverAsync(new JsonObject { ["distribuicaoVagas"] = new JsonArray(recusada, calculada) });

        Item(documento, 0)["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(999);
        Item(documento, 1)["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(10);
    }

    [Fact(DisplayName = "O documento sem distribuição de vagas não consulta nenhum cadastro das vagas")]
    public async Task Descrever_SemDistribuicao_NaoConsultaCadastro()
    {
        await DescreverAsync(new JsonObject());

        await _cenario.Ofertas.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _cenario.Modalidades.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _cenario.Regras.DidNotReceiveWithAnyArgs().ListarPorReferenciasAsync(default!, default);
    }
}
