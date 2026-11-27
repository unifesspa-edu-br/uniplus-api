namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Implementação de <see cref="IFaseCanonicaReader"/> (ADR-0056): leitura direta do
/// banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e o congelamento por valor
/// no consumidor (ADR-0061) dispensa releitura quente. Ordena por <c>Codigo</c>
/// ascendente.
/// </summary>
internal sealed class FaseCanonicaReader : IFaseCanonicaReader
{
    private readonly UniPlusDbContext _dbContext;

    public FaseCanonicaReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FaseCanonicaView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<FaseCanonica> entidades = await _dbContext.FasesCanonicas
            .AsNoTracking()
            .OrderBy(f => f.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<IReadOnlyList<FaseCanonicaView>> ListarVivasPorCodigosAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        List<CodigoFase> codigosDoLote = CodigosDoLote.Validos(codigos, CodigoFase.Criar);
        List<FaseCanonica> entidades = await _dbContext.FasesCanonicas
            .AsNoTracking()
            .Where(f => codigosDoLote.Contains(f.Codigo))
            .OrderBy(f => f.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<FaseCanonicaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        FaseCanonica? entidade = await _dbContext.FasesCanonicas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static FaseCanonicaView ParaView(FaseCanonica f) =>
        new(
            f.Id,
            f.Codigo.Valor,
            f.Nome,
            f.Descricao,
            f.Sinalizadores.AgrupaEtapas,
            f.Sinalizadores.PermiteComplementacao,
            f.BaseLegal,
            f.Sinalizadores.ColetaInscricao,
            f.Sinalizadores.ColetaSolicitacaoIsencao,
            f.Sinalizadores.OrganizadaEmChamadas,
            f.OrigemData.Codigo());
}
