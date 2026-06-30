namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IRecursoAcessibilidadeReader"/> (ADR-0056): leitura
/// direta do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete
/// por convenção). Sem cache — o cadastro é de baixo volume e baixa frequência de
/// leitura viva; o congelamento por valor no consumidor (ADR-0061) dispensa
/// releitura quente (mesmo padrão do <c>TipoDocumentoReader</c>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class RecursoAcessibilidadeReader : IRecursoAcessibilidadeReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public RecursoAcessibilidadeReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RecursoAcessibilidadeView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<RecursoAcessibilidade> entidades = await _dbContext.RecursosAcessibilidade
            .AsNoTracking()
            .OrderBy(r => r.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<RecursoAcessibilidadeView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        RecursoAcessibilidade? entidade = await _dbContext.RecursosAcessibilidade
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static RecursoAcessibilidadeView ParaView(RecursoAcessibilidade r) =>
        new(r.Id, r.Nome);
}
