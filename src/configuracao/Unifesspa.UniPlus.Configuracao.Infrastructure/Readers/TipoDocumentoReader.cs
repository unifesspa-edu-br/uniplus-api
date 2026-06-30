namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="ITipoDocumentoReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e baixa frequência de
/// leitura viva; o congelamento por valor no consumidor (ADR-0061) dispensa
/// releitura quente (mesmo padrão do <c>PesoAreaEnemReader</c>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class TipoDocumentoReader : ITipoDocumentoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoDocumentoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoDocumentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<TipoDocumento> entidades = await _dbContext.TiposDocumento
            .AsNoTracking()
            .OrderBy(t => t.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<TipoDocumentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        TipoDocumento? entidade = await _dbContext.TiposDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static TipoDocumentoView ParaView(TipoDocumento t) =>
        new(
            t.Id,
            t.Codigo,
            t.Nome,
            CategoriaDocumentos.ParaTokenCanonico(t.Categoria));
}
