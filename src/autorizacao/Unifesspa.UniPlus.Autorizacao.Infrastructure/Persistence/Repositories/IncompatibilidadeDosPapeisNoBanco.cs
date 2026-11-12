namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A trava por pessoa como trava consultiva da transação, uma por par emissor e <c>sub</c>, e a conferência dos papéis
/// incompatíveis sobre as concessões vigentes da instituição.
/// </summary>
/// <remarks>
/// A chave é o resumo de 64 bits do par, separado por quebra de linha, que nenhum emissor nem <c>sub</c> contém. Duas
/// pessoas com o mesmo resumo só se serializam entre si, sem afetar a correção.
/// </remarks>
public sealed class IncompatibilidadeDosPapeisNoBanco : IIncompatibilidadeDosPapeis
{
    private readonly AutorizacaoDbContext _context;

    public IncompatibilidadeDosPapeisNoBanco(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task TravarAsync(IEnumerable<Identidade> pessoas, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pessoas);

        foreach (Identidade pessoa in pessoas.Distinct().OrderBy(static p => p.Emissor, StringComparer.Ordinal).ThenBy(static p => p.Sub, StringComparer.Ordinal))
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended('autorizacao.pessoa:' || {0} || chr(10) || {1}, 0))",
                [pessoa.Emissor, pessoa.Sub],
                cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<bool> TemPapelIncompativelAsync(
        Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return PapeisIncompativeis.PerfilIncompativelCom(perfilId) is { } incompativel
            ? _context.Concessoes
                .AsNoTracking()
                .VigentesDaInstituicao(agora)
                .AnyAsync(
                    c => c.PerfilId == incompativel && c.BeneficiarioEmissor == pessoa.Emissor && c.BeneficiarioSub == pessoa.Sub,
                    cancellationToken)
            : Task.FromResult(false);
    }
}
