namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Os titulares de uma permissão, lidos das concessões para quem precisa avisá-los. O perfil desativado
/// continua concedendo as concessões que já existem, como na decisão de acesso, e por isso não é filtrado; o perfil
/// suspenso não concede e não tem titulares. A permissão é a que a concessão dá pela composição em vigor, na versão a que
/// está presa.
/// </summary>
internal sealed class TitularesDaPermissao : ITitularesDaPermissao
{
    private readonly UniPlusDbContext _context;
    private readonly TimeProvider _timeProvider;

    public TitularesDaPermissao(UniPlusDbContext context, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default) =>
        [.. (await ListarTitularesComEmailAsync(codigoDaPermissao, cancellationToken).ConfigureAwait(false)).Select(static t => t.Email).Distinct()];

    public async Task<IReadOnlyList<TitularDaPermissao>> ListarTitularesComEmailAsync(
        string codigoDaPermissao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDaPermissao);

        return CodigoDePermissao.TentarCriar(codigoDaPermissao, out CodigoDePermissao? codigo)
            ? await TitularesDoCodigoAsync(codigo, cancellationToken).ConfigureAwait(false)
            : [];
    }

    private async Task<IReadOnlyList<TitularDaPermissao>> TitularesDoCodigoAsync(CodigoDePermissao codigo, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _timeProvider.GetUtcNow();

        return await (
            from c in _context.Concessoes.AsNoTracking().VigentesDaInstituicao(agora)
            where c.BeneficiarioEmail != null
                && _context.ConcessoesComPermissaoEmVigor(codigo, agora).Contains(c.Id)
            select new TitularDaPermissao(c.BeneficiarioEmissor, c.BeneficiarioContaId, c.BeneficiarioEmail!))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
