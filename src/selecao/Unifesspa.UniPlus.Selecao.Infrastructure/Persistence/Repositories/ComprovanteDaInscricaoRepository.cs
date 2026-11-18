namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

/// <summary>Protege o conteúdo dos comprovantes; as consultas de metadados não carregam nem decifram as respostas.</summary>
public sealed class ComprovanteDaInscricaoRepository : IComprovanteDaInscricaoRepository
{
    private readonly UniPlusDbContext _dbContext;
    private readonly SensiveisDoComprovante _sensiveis;

    public ComprovanteDaInscricaoRepository(UniPlusDbContext dbContext, IUniPlusEncryptionService encryption, IUniPlusBlindIndexService blindIndex)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(blindIndex);
        _dbContext = dbContext;
        _sensiveis = new SensiveisDoComprovante(encryption, blindIndex);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ObterPrimeirosEnviosAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);

        return await _dbContext.ComprovantesInscricao
            .AsNoTracking()
            .Where(c => inscricoes.Contains(c.InscricaoId))
            .GroupBy(static c => c.InscricaoId)
            .Select(static g => new { InscricaoId = g.Key, Primeiro = g.Min(static c => c.Instante) })
            .ToDictionaryAsync(static g => g.InscricaoId, static g => g.Primeiro, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ComprovanteNaPosicao?> ObterComprovanteAsync(
        Guid inscricaoId, int numero, CancellationToken cancellationToken = default)
    {
        IQueryable<ComprovanteInscricao> daInscricao = _dbContext.ComprovantesInscricao.AsNoTracking().Where(c => c.InscricaoId == inscricaoId);
        int total = await daInscricao.CountAsync(cancellationToken).ConfigureAwait(false);
        if (numero < 1 || numero > total)
        {
            return null;
        }

        ComprovanteInscricao comprovante = await EmOrdemDeEmissao(daInscricao).Skip(numero - 1).Take(1)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        await _sensiveis.RestaurarAsync(comprovante, cancellationToken).ConfigureAwait(false);

        return new ComprovanteNaPosicao(comprovante, numero, total);
    }

    public async Task<IReadOnlyList<EmissaoDoComprovante>> ObterEmissoesAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);

        return await EmOrdemDeEmissao(_dbContext.ComprovantesInscricao.AsNoTracking().Where(c => inscricoes.Contains(c.InscricaoId)))
            .Select(static c => new EmissaoDoComprovante(c.InscricaoId, c.Instante))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarComprovanteAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comprovante);

        await _sensiveis.GuardarAsync(comprovante, cancellationToken).ConfigureAwait(false);
        _dbContext.ComprovantesInscricao.Add(comprovante);
    }

    /// <summary>A ordem que dá o número público do comprovante: o instante da emissão e, no empate, o identificador.</summary>
    private static IOrderedQueryable<ComprovanteInscricao> EmOrdemDeEmissao(IQueryable<ComprovanteInscricao> comprovantes) =>
        comprovantes.OrderBy(static c => c.Instante).ThenBy(static c => c.Id);
}
