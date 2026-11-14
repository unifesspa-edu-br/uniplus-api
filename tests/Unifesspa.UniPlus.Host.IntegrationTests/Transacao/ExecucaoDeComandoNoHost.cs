namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Tracking;

/// <summary>O desfecho de um comando executado pelo bus real do Host: o resultado, a exceção que escapou e o que o Wolverine enviou.</summary>
internal sealed class ExecucaoDeComandoNoHost
{
    public ITrackedSession? Sessao { get; private set; }

    public Result? Resultado { get; private set; }

    public Exception? Excecao { get; private set; }

    /// <summary>Executa o comando pelo bus e espera o Wolverine terminar tudo o que ele disparou, o envio do evento inclusive.</summary>
    public static async Task<ExecucaoDeComandoNoHost> ExecutarAsync(IServiceProvider servicos, ICommand<Result> comando)
    {
        ExecucaoDeComandoNoHost execucao = new();
        execucao.Sessao = await servicos.GetRequiredService<IHost>()
            .TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(_ => execucao.EnviarAsync(servicos, comando));

        return execucao;
    }

    private async Task EnviarAsync(IServiceProvider servicos, ICommand<Result> comando)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        try
        {
            Resultado = await escopo.ServiceProvider.GetRequiredService<ICommandBus>().Send(comando, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            Excecao = ex;
        }
    }
}
