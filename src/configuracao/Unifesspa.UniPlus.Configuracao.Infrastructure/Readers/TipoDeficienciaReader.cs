namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="ITipoDeficienciaReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e baixa frequência de
/// leitura viva; o congelamento por valor no consumidor (ADR-0061) dispensa
/// releitura quente (mesmo padrão do <c>TipoDocumentoReader</c>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class TipoDeficienciaReader : ITipoDeficienciaReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoDeficienciaReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoDeficienciaView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<TipoDeficiencia> entidades = await _dbContext.TiposDeficiencia
            .AsNoTracking()
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<TipoDeficienciaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        TipoDeficiencia? entidade = await _dbContext.TiposDeficiencia
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static TipoDeficienciaView ParaView(TipoDeficiencia t) =>
        new(t.Id, t.Nome, t.Descricao, t.Permanente);
}
