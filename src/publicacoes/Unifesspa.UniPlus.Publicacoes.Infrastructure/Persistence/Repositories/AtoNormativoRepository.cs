namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using System.Linq;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
public sealed class AtoNormativoRepository : IAtoNormativoRepository
{
    private readonly PublicacoesDbContext _dbContext;

    public AtoNormativoRepository(PublicacoesDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AdicionarAsync(AtoNormativo ato, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);
        await _dbContext.AtosNormativos.AddAsync(ato, cancellationToken).ConfigureAwait(false);
    }

    public Task<AtoNormativo?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.AtosNormativos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public Task<AtoNormativo?> ObterRetificadorAsync(Guid atoRetificadoId, CancellationToken cancellationToken)
    {
        // O índice único parcial em ato_retificado_id garante no máximo um; aqui a
        // leitura é AsNoTracking porque serve só à mensagem que nomeia o retificador.
        return _dbContext.AtosNormativos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AtoRetificadoId == atoRetificadoId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListarIdsDaCadeiaAsync(Guid atoId, CancellationToken cancellationToken)
    {
        // A cadeia de retificação é linear (garantida pelo índice único parcial em
        // ato_retificado_id) e acíclica (append-only, CHECK de não-autorreferência).
        // Sobe de atoId até a raiz e desce dela até a cabeça, devolvendo a linhagem
        // inteira. A recursão em SQL é necessária: a profundidade é arbitrária e um
        // JOIN fixo não a cobre. O parâmetro é interpolado com segurança pelo EF.
        FormattableString sql = $"""
            WITH RECURSIVE ancestrais AS (
                SELECT id, ato_retificado_id
                FROM publicacoes.ato_normativo
                WHERE id = {atoId}
                UNION ALL
                SELECT a.id, a.ato_retificado_id
                FROM publicacoes.ato_normativo a
                JOIN ancestrais an ON a.id = an.ato_retificado_id
            ),
            cadeia AS (
                SELECT id, ato_retificado_id
                FROM publicacoes.ato_normativo
                WHERE id IN (SELECT id FROM ancestrais WHERE ato_retificado_id IS NULL)
                UNION ALL
                SELECT a.id, a.ato_retificado_id
                FROM publicacoes.ato_normativo a
                JOIN cadeia c ON a.ato_retificado_id = c.id
            )
            SELECT id AS "Value" FROM cadeia
            """;

        return await _dbContext.Database
            .SqlQuery<Guid>(sql)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListarIdsComMesmaNumeracaoAsync(
        string orgao,
        string serie,
        int ano,
        string numero,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orgao);
        ArgumentException.ThrowIfNullOrWhiteSpace(serie);
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);

        string orgaoNorm = orgao.Trim();
        string serieNorm = serie.Trim();
        string numeroNorm = numero.Trim();

        return await _dbContext.AtosNormativos
            .AsNoTracking()
            .Where(a => a.Orgao == orgaoNorm
                && a.Serie == serieNorm
                && a.Ano == ano
                && a.Numero == numeroNorm
                && (excluirId == null || a.Id != excluirId))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<AtoNormativo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena por Id (Guid v7) e
        // fatia, devolvendo as âncoras de prev/next.
        IQueryable<AtoNormativo> query = _dbContext.AtosNormativos.AsNoTracking();

        CursorKeysetPage<AtoNormativo> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }
}
