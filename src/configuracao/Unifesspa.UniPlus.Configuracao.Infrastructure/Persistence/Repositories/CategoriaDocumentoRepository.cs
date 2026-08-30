namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class CategoriaDocumentoRepository : ICategoriaDocumentoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CategoriaDocumentoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<CategoriaDocumento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CategoriasDocumento
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<CategoriaDocumento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CategoriasDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AdicionarAsync(CategoriaDocumento categoria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        await _dbContext.CategoriasDocumento.AddAsync(categoria, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(CategoriaDocumento categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        _dbContext.CategoriasDocumento.Remove(categoria);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato nunca tem categoria viva — evita query
        // desnecessária e garante que a comparação use o valor canônico normalizado
        // (Trim). Case-sensitive (default do Postgres) — alinhado ao índice único.
        Result<CodigoCategoriaDocumento> codigoResult = CodigoCategoriaDocumento.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        CodigoCategoriaDocumento codigoVo = codigoResult.Value!;

        return _dbContext.CategoriasDocumento
            .AsNoTracking()
            .Where(c => excluirId == null || c.Id != excluirId)
            .AnyAsync(c => c.Codigo == codigoVo, cancellationToken);
    }
}
