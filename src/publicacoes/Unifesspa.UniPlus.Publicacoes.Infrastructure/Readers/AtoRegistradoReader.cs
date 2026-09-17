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

    public async Task<bool> EstaRegistradoAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        // Existência pela chave primária, sem materializar o ato: quem pergunta só decide
        // visibilidade, e trazer o agregado exporia a Seleção atributos documentais que ela
        // deliberadamente não guarda (ADR-0108).
        return await _db.Set<AtoNormativo>()
            .AsNoTracking()
            .AnyAsync(a => a.Id == atoId, cancellationToken)
            .ConfigureAwait(false);
    }
}
