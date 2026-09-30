namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="ITermoConsentimentoReader"/> (ADR-0056). A junção com o termo aplica
/// o filtro de exclusão lógica dele: versão de termo removido não é oferecida a vínculo novo.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em DependencyInjection.")]
internal sealed class TermoConsentimentoReader : ITermoConsentimentoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TermoConsentimentoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<VersaoTermoConsentimentoView>> ListarVersoesAsync(
        IReadOnlyCollection<Guid> versaoIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versaoIds);
        if (versaoIds.Count == 0)
        {
            return [];
        }

        var linhas = await _dbContext.VersoesTermoConsentimento.AsNoTracking()
            .Where(v => versaoIds.Contains(v.Id))
            .Join(_dbContext.TermosConsentimento, v => v.TermoConsentimentoId, t => t.Id, (v, t) => new
            {
                v.TermoConsentimentoId,
                v.Id,
                t.Nome,
                v.Texto,
                v.BaseLegal,
                v.FormaAceite,
                v.Hash,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. linhas.Select(static l => new VersaoTermoConsentimentoView(
            l.TermoConsentimentoId, l.Id, l.Nome, l.Texto, l.BaseLegal, FormasAceite.ParaTokenCanonico(l.FormaAceite), l.Hash))];
    }
}
