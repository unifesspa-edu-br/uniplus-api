namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Wolverine;
using Wolverine.Postgresql;

/// <summary>
/// Host que descobre os handlers de teste da transação e roteia o evento deles a uma fila PostgreSQL durável sem escuta: o
/// envelope fica na fila, e o teste o encontra. Um por classe de teste, como o host do conflito na gravação: o handler e a
/// fila existem só neste host, e mantê-lo vivo depois da classe faria o runtime dele disputar as filas dos demais.
/// </summary>
public sealed class HostComHandlersDeTransacao : IDisposable
{
    /// <summary>A fila durável do evento de teste, no schema das filas do Wolverine.</summary>
    public const string TabelaDaFila = "wolverine_queues.wolverine_queue_gravacao_em_dois_modulos";

    private const string Fila = "gravacao-em-dois-modulos";

    private readonly Lock _trava = new();
    private WebApplicationFactory<HostAssemblyMarker>? _host;

    internal IServiceProvider Servicos(MonolitoPostgresFixture fixture)
    {
        lock (_trava)
        {
            _host ??= fixture.Factory.WithWebHostBuilder(
                builder => builder.ConfigureTestServices(static servicos => servicos.ConfigureWolverine(Configurar)));

            return _host.Services;
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
    }

    private static void Configurar(WolverineOptions opcoes)
    {
        opcoes.Discovery.IncludeType(typeof(GravarEmDoisModulosHandler));
        opcoes.Discovery.IncludeType(typeof(RegistrarAtoPelaPortaHandler));
        opcoes.PublishMessage<GravacaoEmDoisModulosConcluida>().ToPostgresqlQueue(Fila);
    }
}
