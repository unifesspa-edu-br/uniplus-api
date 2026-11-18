namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O predicado das revisões das concessões efetivamente abertas, comum à decisão de acesso e ao monitoramento.</summary>
internal static class RevisoesAbertas
{
    /// <summary>
    /// As revisões não encerradas, com o prazo por vencer e ao menos uma concessão elegível: ainda não revogada, não
    /// vencida e fora da versão-alvo.
    /// </summary>
    public static IQueryable<RevisaoDasConcessoesDoPerfil> EfetivamenteAbertas(this UniPlusDbContext context, DateTimeOffset agora) =>
        context.RevisoesDasConcessoes.AsNoTracking()
            .Where(j => j.EncerradaEm == null && j.TerminaEm > agora
                && context.Set<Concessao>().Vigentes(agora).Any(c => c.PerfilId == j.PerfilId && c.VersaoId != j.VersaoAlvoId));
}
