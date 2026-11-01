namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Os titulares de uma permissão, lidos das concessões para quem precisa avisá-los. O perfil desativado
/// continua concedendo as concessões que já existem, como na decisão de acesso, e por isso não é filtrado.
/// </summary>
public sealed class TitularesDaPermissao : ITitularesDaPermissao
{
    private readonly AutorizacaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public TitularesDaPermissao(AutorizacaoDbContext context, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDaPermissao);

        DateTimeOffset agora = _timeProvider.GetUtcNow();

        return await (
            from c in _context.Concessoes.AsNoTracking().VigentesDaInstituicao(agora)
            join p in _context.Perfis.AsNoTracking() on c.PerfilId equals p.Id
            where c.BeneficiarioEmail != null
                && p.Permissoes.Any(x => x.Codigo == codigoDaPermissao)
            select c.BeneficiarioEmail!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
