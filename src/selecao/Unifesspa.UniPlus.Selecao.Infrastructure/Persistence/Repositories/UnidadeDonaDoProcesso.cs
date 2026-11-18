namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Lê a unidade dona numa consulta só, <c>UNION ALL</c> da linha do processo e da do rascunho. O identificador é um
/// só e a publicação apaga o rascunho na mesma transação em que cria o processo, então no máximo uma das linhas existe.
/// </summary>
/// <remarks>
/// Público de propósito, pelo mesmo motivo de <see cref="RascunhoDeProcessoSeletivoRepository"/>: handlers recebem a porta.
/// </remarks>
public sealed class UnidadeDonaDoProcesso : IUnidadeDonaDoProcesso
{
    private readonly UniPlusDbContext _context;

    public UnidadeDonaDoProcesso(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public Task<Guid?> DoProcessoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ProcessosSeletivos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => (Guid?)p.UnidadeAdministradoraOrigemId)
            .Concat(_context.RascunhosDeProcessosSeletivos
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => (Guid?)r.UnidadeAdministradoraOrigemId))
            .FirstOrDefaultAsync(cancellationToken);
}
