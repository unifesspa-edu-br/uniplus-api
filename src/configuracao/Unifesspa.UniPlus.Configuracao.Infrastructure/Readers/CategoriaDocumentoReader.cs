namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="ICategoriaDocumentoReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e o congelamento por valor no
/// consumidor (ADR-0061) dispensa releitura quente. Ordena por <c>Codigo</c>
/// ascendente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class CategoriaDocumentoReader : ICategoriaDocumentoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CategoriaDocumentoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CategoriaDocumentoView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<CategoriaDocumento> entidades = await _dbContext.CategoriasDocumento
            .AsNoTracking()
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<CategoriaDocumentoView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        CategoriaDocumento? entidade = await _dbContext.CategoriasDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static CategoriaDocumentoView ParaView(CategoriaDocumento c) =>
        new(
            c.Id,
            c.Codigo.Valor,
            c.Nome,
            c.Descricao,
            c.Ordem);
}
