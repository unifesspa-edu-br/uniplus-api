namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O que a retificação deixou no banco, lido por outro escopo e outra conexão que a da retificação: o que outra
/// requisição enxerga depois do commit, ou depois da falha.
/// </summary>
internal static class EstadoDaRetificacaoNoBanco
{
    public static Task<List<VersaoConfiguracao>> VersoesAsync(IServiceProvider servicos, Guid processoId) =>
        ConsultarAsync(servicos, db => db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).OrderBy(v => v.NumeroVersao).ToListAsync());

    public static Task<CertameDivulgado> DivulgacaoAsync(IServiceProvider servicos, Guid processoId, CancellationToken cancellationToken = default) =>
        ConsultarAsync(servicos, db => db.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processoId, cancellationToken));

    public static Task<AtoDoProcesso> AtoDoProcessoAsync(IServiceProvider servicos, Guid atoId) =>
        ConsultarAsync(servicos, db => db.AtosDoProcesso.AsNoTracking().SingleAsync(a => a.Id == atoId));

    public static Task<int> AtosDoProcessoAsync(IServiceProvider servicos, Guid processoId) =>
        ConsultarAsync(servicos, db => db.AtosDoProcesso.AsNoTracking().CountAsync(a => a.ProcessoSeletivoId == processoId));

    public static Task<bool> AtoDoProcessoExisteAsync(IServiceProvider servicos, Guid atoId) =>
        ConsultarAsync(servicos, db => db.AtosDoProcesso.AsNoTracking().AnyAsync(a => a.Id == atoId));

    public static Task<bool> AtoNormativoExisteAsync(IServiceProvider servicos, Guid atoId) =>
        ConsultarAsync(servicos, db => db.Set<AtoNormativo>().AsNoTracking().AnyAsync(a => a.Id == atoId));

    public static Task<int> RetificacoesNoRegistroCentralAsync(IServiceProvider servicos, Guid atoRetificadoId) =>
        ConsultarAsync(servicos, db => db.Set<AtoNormativo>().AsNoTracking().CountAsync(a => a.AtoRetificadoId == atoRetificadoId));

    /// <summary>As mensagens pendentes de envio (nas filas duráveis de entrada e de saída) que citam o identificador.</summary>
    public static Task<int> MensagensPendentesQueCitamAsync(IServiceProvider servicos, Guid identificador) =>
        ConsultarAsync(servicos, db => db.Database.SqlQuery<int>($"""
            SELECT (SELECT count(*) FROM wolverine.wolverine_outgoing_envelopes WHERE position(convert_to({identificador.ToString("D")}, 'UTF8') in body) > 0)
                 + (SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE position(convert_to({identificador.ToString("D")}, 'UTF8') in body) > 0) AS "Value"
            """).SingleAsync());

    /// <summary>Os tipos das mensagens pendentes de envio que citam o processo; o tipo não muda quando o envio move a mensagem de uma tabela para a outra.</summary>
    public static async Task<IReadOnlyList<string>> MensagensPendentesDoProcessoAsync(IServiceProvider servicos, Guid processoId, CancellationToken cancellationToken = default) =>
        await ConsultarAsync(servicos, db => db.Database.SqlQuery<string>($"""
            SELECT message_type AS "Value" FROM wolverine.wolverine_outgoing_envelopes WHERE position(convert_to({processoId.ToString("D")}, 'UTF8') in body) > 0
            UNION ALL
            SELECT message_type AS "Value" FROM wolverine.wolverine_incoming_envelopes WHERE position(convert_to({processoId.ToString("D")}, 'UTF8') in body) > 0
            """).ToListAsync(cancellationToken));

    /// <summary>
    /// Espera os registros duráveis de entrega do evento e da cópia estarem tratados e a cópia registrar o endereço.
    /// O transporte Kafka está desabilitado nesta fixture, portanto a projeção Avro não deixa mensagem durável aqui.
    /// </summary>
    public static async Task<IReadOnlyList<string>> MensagensPendentesAssentadasAsync(IServiceProvider servicos, Guid processoId, Guid atoId)
    {
        using CancellationTokenSource prazo = new(TimeSpan.FromSeconds(60));
        try
        {
            while (!await PublicacaoTratadaAsync(servicos, processoId, atoId, prazo.Token))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), prazo.Token);
            }

            return await MensagensPendentesDoProcessoAsync(servicos, processoId, prazo.Token);
        }
        catch (OperationCanceledException cancelamento) when (prazo.IsCancellationRequested)
        {
            throw new TimeoutException($"A publicação do processo {processoId} não concluiu a entrega do evento, a cópia do ato {atoId} e o processamento das mensagens em 60 segundos.", cancelamento);
        }
    }

    private static async Task<bool> PublicacaoTratadaAsync(IServiceProvider servicos, Guid processoId, Guid atoId, CancellationToken cancellationToken)
    {
        bool copiado = (await DivulgacaoAsync(servicos, processoId, cancellationToken)).CopiouOsDocumentosDoAto(atoId);
        return copiado && await EntregasTratadasAsync(servicos, processoId, cancellationToken) == 2;
    }

    private static Task<int> EntregasTratadasAsync(IServiceProvider servicos, Guid processoId, CancellationToken cancellationToken) =>
        ConsultarAsync(servicos, db => db.Database.SqlQuery<int>($"""
            SELECT count(DISTINCT message_type)::int AS "Value" FROM wolverine.wolverine_incoming_envelopes
            WHERE status = 'Handled' AND position(convert_to({processoId.ToString("D")}, 'UTF8') in body) > 0
              AND message_type IN ({typeof(ProcessoPublicadoEvent).FullName}, {typeof(CopiarDocumentosDoAtoAoAcervo).FullName})
            """).SingleAsync(cancellationToken));

    /// <summary>
    /// Espera o banco registrar uma sessão de <paramref name="processosDaEsperante"/> parada na trava do processo seletivo (o
    /// bloqueio da linha do processo) retida pela conexão <paramref name="pidDaQuemSegura"/>: prova que é a segunda retificação, e
    /// não outra operação, que espera pela primeira. O limite cancela a consulta em andamento, e é menor que a espera da trava, para a
    /// falha da sondagem aparecer antes da recusa da segunda retificação.
    /// </summary>
    public static async Task AguardarSessaoEsperandoPelaTravaDeAsync(
        IServiceProvider servicos, int pidDaQuemSegura, Func<IReadOnlyCollection<int>> processosDaEsperante, TimeSpan limite)
    {
        using CancellationTokenSource prazo = new(limite);
        try
        {
            while (await SessoesEsperandoPelaTravaDeAsync(servicos, pidDaQuemSegura, [.. processosDaEsperante()], prazo.Token) == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), prazo.Token);
            }
        }
        catch (OperationCanceledException cancelamento) when (prazo.IsCancellationRequested)
        {
            throw new TimeoutException($"Nenhuma sessão da segunda retificação ficou esperando pela trava do processo seletivo retida pela conexão {pidDaQuemSegura}.", cancelamento);
        }
    }

    private static Task<int> SessoesEsperandoPelaTravaDeAsync(IServiceProvider servicos, int pidDaQuemSegura, int[] processosDaEsperante, CancellationToken cancellationToken) =>
        ConsultarAsync(servicos, db => db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM pg_stat_activity
            WHERE datname = current_database()
              AND pid = ANY({processosDaEsperante})
              AND {pidDaQuemSegura} = ANY(pg_blocking_pids(pid))
              AND query LIKE '%selecao.processos_seletivos%FOR UPDATE%'
            """).SingleAsync(cancellationToken));

    private static async Task<T> ConsultarAsync<T>(IServiceProvider servicos, Func<UniPlusDbContext, Task<T>> consulta)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();

        return await consulta(escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }
}
