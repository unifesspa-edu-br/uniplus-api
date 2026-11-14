namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Grava na base o estado anterior à regra que tirou o prazo da designação de Representante do controlador
/// (UNI-REQ-0200, UNI-REQ-0394): a concessão de Representante com prazo, vigente ou vencida, e o pedido de Representante
/// com o prazo que o gestor definiu. O domínio já não produz nenhum dos dois; o apoio concede ou cria pelo caminho real,
/// sem prazo, e só então grava o prazo direto na linha, sem relaxar a regra do domínio.
/// </summary>
public static class EstadoAnteriorAoRepresentanteSemPrazo
{
    /// <summary>
    /// Concede o perfil de Representante à pessoa e grava na concessão o prazo informado, com o início um dia antes dele
    /// ou do instante atual, o que for anterior. Concessão igual não revogada é reaproveitada e recebe o prazo.
    /// </summary>
    public static async Task<Guid> DesignarComPrazoAsync(IServiceProvider services, string sub, DateTimeOffset validaAte, string? email = null)
    {
        Guid id = await ConcessoesDeTeste.ConcederGovernancaAsync(services, sub, TipoDeGovernanca.RepresentanteDoControlador, email);
        await GravarPrazoNaDesignacaoAsync(services, id, validaAte);
        return id;
    }

    /// <summary>Grava o prazo na concessão de Representante, como ela estaria se tivesse sido concedida antes da regra.</summary>
    public static Task GravarPrazoNaDesignacaoAsync(IServiceProvider services, Guid concessaoId, DateTimeOffset validaAte)
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        DateTimeOffset concedidaEm = (validaAte < agora ? validaAte : agora).AddDays(-1);

        return ExecutarAsync(
            services,
            "UPDATE autorizacao.concessoes SET concedida_em = {1}, valida_ate = {2} WHERE id = {0} AND perfil_id = {3}",
            concessaoId, concedidaEm, validaAte, PerfisDeSistema.RepresentanteDoControladorId);
    }

    /// <summary>Grava no pedido de Representante o prazo que o gestor teria definido antes da regra.</summary>
    public static Task GravarPrazoNoPedidoAsync(IServiceProvider services, Guid pedidoId, DateTimeOffset validaAte) =>
        ExecutarAsync(
            services,
            "UPDATE autorizacao.pedidos_de_concessao SET valida_ate = {1} WHERE id = {0} AND perfil_id = {2}",
            pedidoId, validaAte, PerfisDeSistema.RepresentanteDoControladorId);

    private static async Task ExecutarAsync(IServiceProvider services, string sql, params object[] parametros)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        int linhas = await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
        if (linhas != 1)
        {
            throw new InvalidOperationException($"O estado anterior à regra não foi gravado: {linhas} linhas alteradas.");
        }
    }
}
