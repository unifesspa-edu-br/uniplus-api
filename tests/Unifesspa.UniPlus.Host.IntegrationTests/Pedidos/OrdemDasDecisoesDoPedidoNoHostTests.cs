namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// Duas decisões do mesmo pedido gravadas no mesmo microssegundo saem na ordem em que foram registradas, tanto na leitura
/// do pedido quanto no contrato, mesmo quando a registrada por último tem o identificador menor.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class OrdemDasDecisoesDoPedidoNoHostTests : PedidosDeConcessaoTestBase
{
    private const string Inserir = """
        INSERT INTO autorizacao.pedido_decisoes
            (id, pedido_id, etapa, resultado, decisor_emissor, decisor_sub, decisor_nome, instante, permissoes_avaliadas, sensibilidade_avaliada)
        VALUES ({0}, {1}, 'gestor', 'aprovado', 'https://idp.test', {2}, {3}, {4}, ARRAY['selecao:processos:consultar'], 'interno')
        """;

    private static readonly DateTimeOffset MesmoInstante = new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    public OrdemDasDecisoesDoPedidoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Com o mesmo instante, as decisões do pedido saem pela ordem de registro, e não pelo identificador")]
    public async Task MesmoInstante_PelaOrdemDeRegistro()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, perfil, unidade, null);
        (Guid menor, Guid maior) = PorIdentificador(Guid.CreateVersion7(), Guid.CreateVersion7());
        await InserirAsync(pedido, maior, "Primeira decisão");
        await InserirAsync(pedido, menor, "Segunda decisão");

        PedidoDeConcessao lido = await LerPedidoAsync(pedido);
        JsonElement contrato = await LerAsync(betina, $"{Rota}/{pedido}");

        lido.Decisoes.Select(d => d.Id).Should().Equal(maior, menor);
        NomesDosDecisores(contrato).Should().Equal("Primeira decisão", "Segunda decisão");
        CamposDasDecisoes(contrato).Should().NotContain("ordemDeRegistro");
    }

    private static IEnumerable<string?> NomesDosDecisores(JsonElement contrato) =>
        contrato.GetProperty("decisoes").EnumerateArray().Select(static d => d.GetProperty("decisor").GetProperty("nome").GetString());

    private static IEnumerable<string> CamposDasDecisoes(JsonElement contrato) =>
        contrato.GetProperty("decisoes").EnumerateArray().SelectMany(static d => d.EnumerateObject()).Select(static p => p.Name);

    // O Postgres ordena o uuid pela forma textual, que é a ordem do desempate antigo pelo identificador.
    private static (Guid Menor, Guid Maior) PorIdentificador(Guid um, Guid outro) =>
        string.CompareOrdinal(um.ToString(), outro.ToString()) < 0 ? (um, outro) : (outro, um);

    private Task InserirAsync(Guid pedidoId, Guid decisaoId, string nome) =>
        ExecutarSqlAsync(Inserir, decisaoId, pedidoId, Diogo.Sub.ToString(), nome, MesmoInstante);

    private async Task<PedidoDeConcessao> LerPedidoAsync(Guid pedidoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IPedidoDeConcessaoRepository>().ObterParaLeituraAsync(pedidoId))!;
    }
}
