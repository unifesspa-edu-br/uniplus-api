namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Implementação do <see cref="IVagaDeLinhagemReader"/> (ADR-0056/0107).
/// </summary>
/// <remarks>
/// <b>internal</b> pelo mesmo motivo do <see cref="TipoAtoPublicadoReader"/>: público, o
/// codegen do Wolverine tentaria construí-lo inline nos handlers de Seleção e enxergaria o
/// <c>PublicacoesDbContext</c> na árvore — <i>"multiple DbContext types detected"</i>.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
internal sealed class VagaDeLinhagemReader(PublicacoesDbContext db) : IVagaDeLinhagemReader
{
    private readonly PublicacoesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<bool> ObjetoJaTemAtoDeOutraLinhagemAsync(
        string entidadeTipo,
        Guid entidadeId,
        string tipoCodigo,
        IReadOnlyCollection<Guid> idsDaPropriaLinhagem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idsDaPropriaLinhagem);

        Guid[] daPropriaLinhagem = [.. idsDaPropriaLinhagem];

        // A MESMA pergunta que o registro faz (AtoNormativoRepository.ObterAtoConflitanteNoObjetoAsync):
        // existe ato deste tipo vinculado a este objeto, fora da minha linhagem? Olha o
        // HISTÓRICO, e não só a tabela de vagas, porque `unico_por_objeto` é editável — um ato
        // registrado quando o tipo ainda não era único não reservou vaga, e mesmo assim conflita
        // se o tipo passar a ser único depois.
        return await _db.Set<AtoNormativo>()
            .AsNoTracking()
            .AnyAsync(
                a => a.TipoCodigo == tipoCodigo
                    && !daPropriaLinhagem.Contains(a.Id)
                    && _db.Set<VinculoAtoEntidade>()
                        .Any(v => v.AtoId == a.Id && v.EntidadeTipo == entidadeTipo && v.EntidadeId == entidadeId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> AtoJaFoiRetificadoAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        return await _db.Set<AtoNormativo>()
            .AsNoTracking()
            .AnyAsync(a => a.AtoRetificadoId == atoId, cancellationToken)
            .ConfigureAwait(false);
    }
}
