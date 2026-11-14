namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A cadeia de versões publicadas de um processo, da mais vigente para a mais antiga. É o seletor único da versão
/// vigente: a de maior vigência já iniciada no instante, desempatada pelo número. O relógio pode regredir, e a
/// versão sucessora nasce ancorada na vigência da anterior; por isso o número maior sozinho não diz qual vale.
/// </summary>
internal static class VersoesVigentes
{
    internal static IQueryable<VersaoConfiguracao> Vigentes(this UniPlusDbContext context, Guid processoId, DateTimeOffset instante)
    {
        DateTimeOffset instanteUtc = instante.ToUniversalTime();
        return context.Publicadas(processoId, instanteUtc).Where(v => v.VigenteAPartirDe <= instanteUtc);
    }

    // Todas as versões, as já vigentes primeiro: a unidade e o início das fases se leem do histórico inteiro, e a
    // versão que ainda não vige só entra depois da que vale. O EXISTS através de ProcessosSeletivos herda o filtro de
    // exclusão lógica do processo: a configuração congelada de um processo excluído não vaza.
    internal static IQueryable<VersaoConfiguracao> Publicadas(this UniPlusDbContext context, Guid processoId, DateTimeOffset instante)
    {
        DateTimeOffset instanteUtc = instante.ToUniversalTime();
        return context.VersoesConfiguracao
            .Where(v => v.ProcessoSeletivoId == processoId
                && context.Set<ProcessoSeletivo>().Any(p => p.Id == processoId))
            .OrderBy(v => v.VigenteAPartirDe > instanteUtc)
            .ThenByDescending(v => v.VigenteAPartirDe)
            .ThenByDescending(v => v.NumeroVersao);
    }
}
