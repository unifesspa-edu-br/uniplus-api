namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IFaseCanonicaReader"/> (ADR-0056): leitura direta do
/// banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e o congelamento por valor
/// no consumidor (ADR-0061) dispensa releitura quente. Ordena por <c>Codigo</c>
/// ascendente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class FaseCanonicaReader : IFaseCanonicaReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public FaseCanonicaReader(ConfiguracaoDbContext dbContext)
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
            DonosTipicos.ParaTokenCanonico(f.DonoTipico),
            f.AgrupaEtapas,
            f.PermiteComplementacao,
            f.BaseLegal,
            f.ProduzResultado,
            f.ResultadoDefinitivo,
            f.ColetaInscricao,
            OrigensDataFase.ParaTokenCanonico(f.OrigemData));
}
