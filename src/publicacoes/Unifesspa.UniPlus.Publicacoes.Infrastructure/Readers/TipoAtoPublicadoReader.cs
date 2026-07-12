namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Implementação do <see cref="ITipoAtoPublicadoReader"/> (ADR-0056): resolve o tipo pela
/// vigência, exatamente como o registro do ato faz — janela semiaberta <c>[inicio, fim)</c>.
/// </summary>
/// <remarks>
/// <para>
/// A resolução tem de ser a MESMA do registro. Se divergissem, a pré-validação aprovaria o
/// que o registro depois recusaria, e a recusa voltaria a cair na dead letter — que é
/// justamente o que esta leitura existe para evitar.
/// </para>
/// <para>
/// <b>internal</b> de propósito: o contrato é público, o concreto não. Se fosse público, o
/// codegen do Wolverine tentaria construí-lo inline dentro dos handlers de Seleção, enxergaria
/// o <c>PublicacoesDbContext</c> na árvore e falharia com <i>"multiple DbContext types
/// detected"</i> — o middleware transacional não coordena dois contextos. Sendo internal, o
/// codegen cai em service location, que é o consumo correto de um contrato cross-módulo e que
/// <c>SelecaoCodegenRegistration</c> declara explicitamente (ADR-0098).
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
internal sealed class TipoAtoPublicadoReader(PublicacoesDbContext db) : ITipoAtoPublicadoReader
{
    private readonly PublicacoesDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<TipoAtoPublicadoView?> ObterVigenteAsync(
        string codigo,
        DateOnly dataPublicacao,
        CancellationToken cancellationToken = default)
    {
        return await _db.Set<TipoAtoPublicado>()
            .AsNoTracking()
            .Where(t => t.Codigo == codigo
                && t.VigenciaInicio <= dataPublicacao
                && (t.VigenciaFim == null || t.VigenciaFim > dataPublicacao))
            .Select(t => new TipoAtoPublicadoView(
                t.Codigo,
                t.Nome,
                t.CongelaConfiguracao,
                t.UnicoPorObjeto,
                t.EfeitoIrreversivel))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
