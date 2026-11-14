namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O que os testes de transação conferem direto no banco, numa conexão própria: só o que foi confirmado aparece.</summary>
internal static class ConsultasDaTransacaoNoBanco
{
    internal static Task<int> MotivosAsync(IServiceProvider servicos, string codigo) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM selecao.motivos_de_decisao WHERE codigo = {codigo}").SingleAsync());

    internal static Task<int> TiposAsync(IServiceProvider servicos, string codigo) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM publicacoes.tipo_ato_publicado WHERE codigo = {codigo}").SingleAsync());

    internal static Task<int> AtosAsync(IServiceProvider servicos, Guid atoId) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM publicacoes.ato_normativo WHERE id = {atoId}").SingleAsync());

    internal static Task<int> VagasDoObjetoAsync(IServiceProvider servicos, Guid objetoId) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM publicacoes.vagas_de_linhagem WHERE entidade_id = {objetoId}").SingleAsync());

    /// <summary>A transação que gravou a linha: duas linhas com o mesmo valor foram gravadas pela mesma transação.</summary>
    internal static Task<long> TransacaoDoMotivoAsync(IServiceProvider servicos, string codigo) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM selecao.motivos_de_decisao WHERE codigo = {codigo}").SingleAsync());

    internal static Task<long> TransacaoDoAtoAsync(IServiceProvider servicos, Guid atoId) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM publicacoes.ato_normativo WHERE id = {atoId}").SingleAsync());

    internal static Task<long> TransacaoDoTipoAsync(IServiceProvider servicos, string codigo) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM publicacoes.tipo_ato_publicado WHERE codigo = {codigo}").SingleAsync());

    /// <summary>Os envelopes do evento ainda no outbox e os já entregues à fila durável do evento.</summary>
    internal static async Task<int> EnvelopesDoEventoAsync(IServiceProvider servicos, string codigo) =>
        await NoOutboxAsync(servicos, codigo) + await NaFilaAsync(servicos, codigo);

    private static Task<int> NoOutboxAsync(IServiceProvider servicos, string codigo) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE position(convert_to({codigo}, 'UTF8') in body) > 0")
            .SingleAsync());

    private static Task<int> NaFilaAsync(IServiceProvider servicos, string codigo) =>
        ConsultarAsync(servicos, contexto => contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine_queues.wolverine_queue_gravacao_em_dois_modulos WHERE position(convert_to({codigo}, 'UTF8') in body) > 0")
            .SingleAsync());

    private static async Task<T> ConsultarAsync<T>(IServiceProvider servicos, Func<UniPlusDbContext, Task<T>> consulta)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();

        return await consulta(escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }
}
