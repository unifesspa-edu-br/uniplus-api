namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Os titulares de uma permissão, lidos das concessões para quem precisa avisá-los. O perfil desativado
/// continua concedendo as concessões que já existem, como na decisão de acesso, e por isso não é filtrado. A permissão
/// é a da composição em vigor do perfil.
/// </summary>
internal sealed class TitularesDaPermissao : ITitularesDaPermissao
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
            where c.BeneficiarioEmail != null
                && _context.PerfisComPermissaoEmVigor(codigoDaPermissao).Contains(c.PerfilId)
            select c.BeneficiarioEmail!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
