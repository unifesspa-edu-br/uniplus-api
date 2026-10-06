using JasperFx;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace Spike882.Agendamento;

public static class Hosts
{
    /// <summary>
    /// Host Wolverine. Os flags espelham a produção: <c>caminhoProducao</c> liga as transações do
    /// EF com o outbox, <c>somenteLeituraDeSchema</c> espelha o modo Skip (ADR-0127).
    /// </summary>
    public static IHost Criar(
        string conexao,
        bool agendamentoDuravel,
        bool comRetry = false,
        bool somenteLeituraDeSchema = false,
        bool caminhoProducao = false,
        bool filasLocaisDuraveis = false)
    {
        Claim.Conexao = conexao;

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(log => log.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(s => s.AddDbContextWithWolverineIntegration<SpikeDbContext>(x => x.UseNpgsql(conexao)))
            .UseWolverine(opts =>
            {
                opts.PersistMessagesWithPostgresql(conexao, "wolverine").EnableMessageTransport(_ => { });
                opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
                opts.Discovery.IncludeAssembly(typeof(Hosts).Assembly);
                opts.Durability.ScheduledJobPollingTime = TimeSpan.FromSeconds(1);

                if (agendamentoDuravel)
                {
                    opts.Policies.AlwaysMakeScheduledMessagesDurable();
                }

                if (comRetry)
                {
                    opts.OnException<InvalidOperationException>().RetryTimes(3);
                }

                if (somenteLeituraDeSchema)
                {
                    opts.AutoBuildMessageStorageOnStartup = AutoCreate.None;
                }

                if (filasLocaisDuraveis)
                {
                    opts.Policies.UseDurableLocalQueues();

                    // Encurtado só no spike: o prazo de produção decide quanto tempo a mensagem
                    // de um nó morto fica presa antes de ser reatribuída.
                    opts.Durability.StaleNodeTimeout = TimeSpan.FromSeconds(10);
                    opts.Durability.NodeReassignmentPollingTime = TimeSpan.FromSeconds(2);
                    opts.Durability.FirstNodeReassignmentExecution = TimeSpan.FromSeconds(2);
                }

                if (caminhoProducao)
                {
                    opts.UseEntityFrameworkCoreTransactions();
                    opts.Policies.AutoApplyTransactions();
                    opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;
                }
            })
            .Build();
    }
}
