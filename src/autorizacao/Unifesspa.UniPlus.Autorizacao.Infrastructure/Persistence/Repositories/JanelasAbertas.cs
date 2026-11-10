namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>O predicado das janelas de migração efetivamente abertas, comum à decisão de acesso e ao monitoramento.</summary>
internal static class JanelasAbertas
{
    /// <summary>
    /// As janelas não encerradas, com o prazo por vencer e ao menos uma concessão elegível: ainda não revogada, não
    /// vencida e fora da versão-alvo.
    /// </summary>
    public static IQueryable<JanelaDeMigracao> EfetivamenteAbertas(this AutorizacaoDbContext context, DateTimeOffset agora) =>
        context.JanelasDeMigracao.AsNoTracking()
            .Where(j => j.EncerradaEm == null && j.Prazo > agora
                && context.Concessoes.Any(c => c.PerfilId == j.PerfilId && c.RevogadaEm == null
                    && (c.ValidaAte == null || c.ValidaAte > agora) && c.VersaoId != j.VersaoAlvoId));
}
