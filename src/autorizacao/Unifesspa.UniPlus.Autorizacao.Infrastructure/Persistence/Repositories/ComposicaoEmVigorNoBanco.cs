namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// A composição em vigor lida no banco, para as consultas que decidem acesso: no perfil de governança são as
/// permissões próprias; no perfil comum são as da versão da vigência aberta, e o perfil sem vigência aberta não reúne
/// permissão alguma. Espelha <c>Perfil.ComposicaoEmVigor</c>.
/// </summary>
internal static class ComposicaoEmVigorNoBanco
{
    /// <summary>Os perfis cuja composição em vigor reúne a permissão.</summary>
    public static IQueryable<Guid> PerfisComPermissaoEmVigor(this AutorizacaoDbContext context, string codigo)
    {
        IQueryable<Guid> governanca = context.Perfis
            .Where(p => p.Governanca != null && p.Permissoes.Any(x => x.Codigo == codigo))
            .Select(p => p.Id);

        IQueryable<Guid> comuns =
            from v in context.VigenciasDosPerfis
            join ver in context.VersoesDosPerfis on v.VersaoId equals ver.Id
            where v.Ate == null && ver.Permissoes.Any(x => x.Codigo == codigo)
            select v.PerfilId;

        return governanca.Concat(comuns);
    }

    /// <summary>As permissões em vigor de cada perfil informado.</summary>
    public static async Task<Dictionary<Guid, List<string>>> PermissoesEmVigorAsync(
        this AutorizacaoDbContext context, Guid[] perfilIds, CancellationToken cancellationToken)
    {
        List<PermissaoDoPerfil> governanca = await context.PermissoesDeGovernancaAsync(perfilIds, cancellationToken).ConfigureAwait(false);
        List<PermissaoDoPerfil> comuns = await context.PermissoesDasVersoesVigentesAsync(perfilIds, cancellationToken).ConfigureAwait(false);

        return governanca.Concat(comuns)
            .GroupBy(x => x.PerfilId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Codigo).ToList());
    }

    private static Task<List<PermissaoDoPerfil>> PermissoesDeGovernancaAsync(
        this AutorizacaoDbContext context, Guid[] perfilIds, CancellationToken cancellationToken) =>
        context.Perfis
            .AsNoTracking()
            .Where(p => perfilIds.Contains(p.Id) && p.Governanca != null)
            .SelectMany(p => p.Permissoes.Select(x => new PermissaoDoPerfil(p.Id, x.Codigo)))
            .ToListAsync(cancellationToken);

    private static Task<List<PermissaoDoPerfil>> PermissoesDasVersoesVigentesAsync(
        this AutorizacaoDbContext context, Guid[] perfilIds, CancellationToken cancellationToken) =>
        (from v in context.VigenciasDosPerfis.AsNoTracking()
         join ver in context.VersoesDosPerfis.AsNoTracking() on v.VersaoId equals ver.Id
         from x in ver.Permissoes
         where v.Ate == null && perfilIds.Contains(v.PerfilId)
         select new PermissaoDoPerfil(v.PerfilId, x.Codigo))
        .ToListAsync(cancellationToken);

    private sealed record PermissaoDoPerfil(Guid PerfilId, string Codigo);
}
