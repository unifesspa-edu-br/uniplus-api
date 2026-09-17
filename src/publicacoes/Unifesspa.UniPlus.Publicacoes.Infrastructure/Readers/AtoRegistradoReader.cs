namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Implementação do <see cref="IAtoRegistradoReader"/> (ADR-0056).
/// </summary>
/// <remarks>
/// <b>internal</b> pelo mesmo motivo do <see cref="VagaDeLinhagemReader"/>: público, o codegen
/// do Wolverine tentaria construí-lo inline nos handlers de Seleção e enxergaria o
/// <c>PublicacoesDbContext</c> na árvore — <i>"multiple DbContext types detected"</i>.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
internal sealed class AtoRegistradoReader(PublicacoesDbContext db) : IAtoRegistradoReader
{
    private readonly PublicacoesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlySet<Guid>> FiltrarRegistradosAsync(
        IReadOnlyCollection<Guid> atoIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(atoIds);

        if (atoIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        Guid[] procurados = [.. atoIds];

        // Projeção só do identificador, sem materializar o ato: quem pergunta só decide
        // visibilidade, e trazer o agregado exporia à Seleção atributos documentais que ela
        // deliberadamente não guarda (ADR-0108).
        List<Guid> registrados = await _db.Set<AtoNormativo>()
            .AsNoTracking()
            .Where(a => procurados.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<Guid>(registrados);
    }
}
