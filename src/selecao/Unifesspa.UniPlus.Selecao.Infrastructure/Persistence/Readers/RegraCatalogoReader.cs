namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Implementação de <see cref="IRegraCatalogoReader"/>: leitura direta do
/// <c>rol_de_regras</c> (<c>AsNoTracking</c>), sem cache — o catálogo é de
/// baixo volume (dezenas de regras), imutável por versão, e o congelamento por
/// valor no consumidor (<see cref="Domain.ValueObjects.ReferenciaRegra"/> +
/// snapshot, ADR-0061) dispensa releitura quente (mesmo padrão dos readers de
/// reference data do módulo Configuração).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI na registration de Infrastructure do módulo Seleção.")]
internal sealed class RegraCatalogoReader : IRegraCatalogoReader
{
    private readonly SelecaoDbContext _dbContext;

    public RegraCatalogoReader(SelecaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<RegraCatalogo?> ObterAsync(
        string codigo,
        string versao,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RolDeRegras
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Codigo == codigo && r.Versao == versao, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RegraCatalogo>> ListarPorTipoAsync(
        TipoRegra tipo,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RolDeRegras
            .AsNoTracking()
            .Where(r => r.Tipo == tipo)
            .OrderBy(r => r.Codigo)
            .ThenBy(r => r.Versao)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
