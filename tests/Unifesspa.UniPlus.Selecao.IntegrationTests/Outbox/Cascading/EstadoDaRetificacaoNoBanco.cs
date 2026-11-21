namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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

    public static Task<CertameDivulgado> DivulgacaoAsync(IServiceProvider servicos, Guid processoId) =>
        ConsultarAsync(servicos, db => db.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processoId));

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

    /// <summary>A transação (xmin) que gravou cada uma das quatro linhas da retificação: a versão, o ato do processo, o ato central e a divulgação.</summary>
    public static async Task<IReadOnlyList<long>> TransacoesDaRetificacaoAsync(IServiceProvider servicos, Guid processoId, Guid atoId) =>
    [
        await TransacaoAsync(servicos, db => db.Database.SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM selecao.versoes_configuracao WHERE ato_criador_id = {atoId}")),
        await TransacaoAsync(servicos, db => db.Database.SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM selecao.atos_do_processo WHERE id = {atoId}")),
        await TransacaoAsync(servicos, db => db.Database.SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM publicacoes.atos_normativos WHERE id = {atoId}")),
        await TransacaoAsync(servicos, db => db.Database.SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM selecao.certames_divulgados WHERE id = {processoId}")),
    ];

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
    /// As mensagens pendentes depois de a publicação ter terminado de gerar as dela: o tratamento do evento de publicação acrescenta
    /// outra mensagem pouco depois, e quem tira a foto antes disso a veria aparecer como se fosse efeito do que vem a seguir.
    /// </summary>
    public static async Task<IReadOnlyList<string>> MensagensPendentesAssentadasAsync(IServiceProvider servicos, Guid processoId)
    {
        IReadOnlyList<string> anterior = await MensagensPendentesDoProcessoAsync(servicos, processoId);
        for (int tentativa = 0; tentativa < 20; tentativa++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            IReadOnlyList<string> atual = await MensagensPendentesDoProcessoAsync(servicos, processoId);
            if (atual.Order(StringComparer.Ordinal).SequenceEqual(anterior.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                return atual;
            }

            anterior = atual;
        }

        return anterior;
    }

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

    private static Task<long> TransacaoAsync(IServiceProvider servicos, Func<UniPlusDbContext, IQueryable<long>> consulta) =>
        ConsultarAsync(servicos, db => consulta(db).SingleAsync());

    private static async Task<T> ConsultarAsync<T>(IServiceProvider servicos, Func<UniPlusDbContext, Task<T>> consulta)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();

        return await consulta(escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }
}
