namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Wolverine;
using Wolverine.Runtime.Serialization;

/// <summary>
/// As mensagens de remoção dos objetos de um rascunho excluído que o Wolverine guarda no banco: na tabela de envelopes
/// de saída, até o remetente entregá-la à fila, e na fila dos agendados, depois disso. Quem as procura não sabe em qual
/// das duas ela está, então as duas são lidas, e o envelope é identificado pelo rascunho que consta das chaves dele.
/// </summary>
internal static class RemocoesPendentesDoRascunho
{
    public static async Task<IReadOnlyList<Envelope>> ListarAsync(IServiceProvider services, Guid rascunhoId)
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string rascunho = rascunhoId.ToString("D");
        List<byte[]> corpos = [.. await EnviadasAindaNaoEntreguesAsync(db, rascunho), .. await AgendadasNaFilaAsync(db, rascunho)];

        return [.. corpos.Select(EnvelopeSerializer.Deserialize)];
    }

    private static Task<List<byte[]>> EnviadasAindaNaoEntreguesAsync(UniPlusDbContext db, string rascunho) =>
        db.Database.SqlQuery<byte[]>($"""
            SELECT body AS "Value" FROM wolverine.wolverine_outgoing_envelopes
            WHERE position(convert_to({rascunho}, 'UTF8') in body) > 0
            """).ToListAsync();

    private static Task<List<byte[]>> AgendadasNaFilaAsync(UniPlusDbContext db, string rascunho) =>
        db.Database.SqlQuery<byte[]>($"""
            SELECT body AS "Value" FROM wolverine_queues.wolverine_queue_selecao_remocao_arquivos_rascunho_scheduled
            WHERE position(convert_to({rascunho}, 'UTF8') in body) > 0
            """).ToListAsync();
}
