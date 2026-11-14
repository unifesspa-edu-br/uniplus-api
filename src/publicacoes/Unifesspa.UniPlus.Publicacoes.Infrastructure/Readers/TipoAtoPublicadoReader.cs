namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Readers;

using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

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
/// <b>internal</b> de propósito: o contrato é público, o concreto não. Sendo internal, o
/// codegen do Wolverine cai em service location, que é o consumo correto de um contrato
/// cross-módulo e que <c>SelecaoCodegenRegistration</c> declara explicitamente (ADR-0098).
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
internal sealed class TipoAtoPublicadoReader(UniPlusDbContext db) : ITipoAtoPublicadoReader
{
    private readonly UniPlusDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    // A vigência e a projeção são as mesmas nas duas leituras: se divergissem, o lote veria um
    // tipo que a leitura individual não vê, ou o contrário.
    private static readonly Expression<Func<TipoAtoPublicado, TipoAtoPublicadoView>> Projecao = t =>
        new TipoAtoPublicadoView(
            t.Codigo,
            t.Nome,
            t.CongelaConfiguracao,
            t.UnicoPorObjeto,
            t.EfeitoIrreversivel,
            t.EhResultado,
            t.PublicavelComoAvulso,
            t.ExigeAtoCorrigido);

    private IQueryable<TipoAtoPublicado> Vigentes(DateOnly diaDaPublicacao) =>
        _db.Set<TipoAtoPublicado>()
            .AsNoTracking()
            .Where(t => t.VigenciaInicio <= diaDaPublicacao
                && (t.VigenciaFim == null || t.VigenciaFim > diaDaPublicacao));

    public async Task<TipoAtoPublicadoView?> ObterVigenteAsync(
        string codigo,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken = default)
    {
        return await Vigentes(diaDaPublicacao)
            .Where(t => t.Codigo == codigo)
            .Select(Projecao)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TipoAtoPublicadoView>> ListarVigentesAsync(
        IReadOnlyCollection<string> codigos,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        string[] codigosDoLote = [.. codigos];

        return await Vigentes(diaDaPublicacao)
            .Where(t => codigosDoLote.Contains(t.Codigo))
            .Select(Projecao)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
