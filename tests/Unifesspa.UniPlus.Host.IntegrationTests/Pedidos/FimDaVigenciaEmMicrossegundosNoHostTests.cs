namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O instante que o gestor informa no corpo da aprovação entra no sistema em microssegundos, a precisão que o banco
/// persiste: a resposta, a concessão e o que foi enviado com 7 casas decimais só diferem na 7ª, que se descarta.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class FimDaVigenciaEmMicrossegundosNoHostTests : PedidosDeConcessaoTestBase
{
    public FimDaVigenciaEmMicrossegundosNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O fim da vigência enviado com 7 casas decimais é devolvido e gravado em microssegundos")]
    public async Task FimDeVigencia_SetimaCasa_DevolvidoEGravadoEmMicrossegundos()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        DateTimeOffset enviada = ComSetimaCasa(DateTimeOffset.UtcNow.AddMonths(3));
        Guid id = await CriarAsync(betina, perfil, unidade, null);

        JsonElement aprovado = await CorpoAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes", new { validaAte = enviada }), HttpStatusCode.OK);

        DateTimeOffset esperada = enviada.AddTicks(-7);
        aprovado.GetProperty("validaAte").GetDateTimeOffset().Should().Be(esperada);
        aprovado.GetProperty("validaAte").GetString().Should().Contain($".{esperada:ffffff}+00:00");
        (await ConcessoesAsync(Betina, perfil)).Single().ValidaAte.Should().Be(esperada);
    }

    private static DateTimeOffset ComSetimaCasa(DateTimeOffset instante) =>
        new(instante.Ticks - (instante.Ticks % TimeSpan.TicksPerSecond) + 1_234_567, TimeSpan.Zero);
}
