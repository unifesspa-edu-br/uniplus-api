namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using System.Globalization;
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
        // ato_retificado_id) e acíclica (trigger fn_ato_normativo_cadeia_aciclica).
        // Sobe de atoId até a raiz e desce dela até a cabeça, devolvendo a linhagem
        // inteira. A recursão em SQL é necessária: a profundidade é arbitrária e um
        // JOIN fixo não a cobre. O parâmetro é interpolado com segurança pelo EF.
        // CYCLE nos dois sentidos: se um ciclo escapasse da escrita, a leitura o cortaria
        // em vez de pendurar a conexão.
        FormattableString sql = $"""
            WITH RECURSIVE ancestrais AS (
                SELECT id, ato_retificado_id
                FROM publicacoes.ato_normativo
                WHERE id = {atoId}
                UNION ALL
                SELECT a.id, a.ato_retificado_id
                FROM publicacoes.ato_normativo a
                JOIN ancestrais an ON a.id = an.ato_retificado_id
            ) CYCLE id SET ciclo_acima USING caminho_acima,
            cadeia AS (
                SELECT id, ato_retificado_id
                FROM publicacoes.ato_normativo
                WHERE id IN (
                    SELECT id FROM ancestrais WHERE ato_retificado_id IS NULL AND NOT ciclo_acima
                )
                UNION ALL
                SELECT a.id, a.ato_retificado_id
                FROM publicacoes.ato_normativo a
                JOIN cadeia c ON a.ato_retificado_id = c.id
            ) CYCLE id SET ciclo_abaixo USING caminho_abaixo
            SELECT id AS "Value" FROM cadeia WHERE NOT ciclo_abaixo
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

    public async Task<Guid> ObterRaizDaCadeiaAsync(Guid atoId, CancellationToken cancellationToken)
    {
        // Sobe a cadeia até quem não retifica ninguém. O passo é fixo mas a profundidade
        // não — daí SQL recursivo, não JOIN. CYCLE: a aciclicidade é imposta na escrita
        // (trigger fn_ato_normativo_cadeia_aciclica), mas uma leitura que gira para
        // sempre é pior do que uma que devolve erro — a cláusula corta o laço em vez de
        // pendurar a conexão, se algum ciclo escapar.
        FormattableString sql = $"""
            WITH RECURSIVE ancestrais AS (
                SELECT id, ato_retificado_id
                FROM publicacoes.ato_normativo
                WHERE id = {atoId}
                UNION ALL
                SELECT a.id, a.ato_retificado_id
                FROM publicacoes.ato_normativo a
                JOIN ancestrais an ON a.id = an.ato_retificado_id
            ) CYCLE id SET ciclo USING caminho
            SELECT id AS "Value" FROM ancestrais WHERE ato_retificado_id IS NULL AND NOT ciclo
            """;

        List<Guid> raizes = await _dbContext.Database
            .SqlQuery<Guid>(sql)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Zero raízes só acontece se o ato não existe — o handler já o validou antes.
        return raizes.Count == 1
            ? raizes[0]
            : throw new InvalidOperationException(
                $"A cadeia de retificação do ato {atoId} não tem raiz única — a linearidade foi violada no banco.");
    }

    public Task<LinhagemUnicaPorObjeto?> ObterLinhagemDoObjetoAsync(
        string entidadeTipo,
        Guid entidadeId,
        string tipoCodigo,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidadeTipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);

        string tipoNorm = entidadeTipo.Trim();
        string codigoNorm = tipoCodigo.Trim();

        return _dbContext.LinhagensUnicasPorObjeto
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.EntidadeTipo == tipoNorm && l.EntidadeId == entidadeId && l.TipoCodigo == codigoNorm,
                cancellationToken);
    }

    public async Task AdicionarLinhagemAsync(LinhagemUnicaPorObjeto linhagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linhagem);
        await _dbContext.LinhagensUnicasPorObjeto.AddAsync(linhagem, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)>> ListarVinculosDoAtoAsync(
        Guid atoId,
        CancellationToken cancellationToken)
    {
        List<VinculoAtoEntidade> vinculos = await _dbContext.VinculosAtoEntidade
            .AsNoTracking()
            .Where(v => v.AtoId == atoId)
            .OrderBy(v => v.EntidadeTipo)
            .ThenBy(v => v.EntidadeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. vinculos.Select(v => (v.EntidadeTipo, v.EntidadeId))];
    }

    public Task<AtoNormativo?> ObterAtoConflitanteNoObjetoAsync(
        string entidadeTipo,
        Guid entidadeId,
        string tipoCodigo,
        IReadOnlyCollection<Guid> idsDaPropriaCadeia,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidadeTipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);
        ArgumentNullException.ThrowIfNull(idsDaPropriaCadeia);

        string tipoNorm = entidadeTipo.Trim();
        string codigoNorm = tipoCodigo.Trim();
        Guid[] daPropriaCadeia = [.. idsDaPropriaCadeia];

        return _dbContext.AtosNormativos
            .AsNoTracking()
            .Where(a => a.TipoCodigo == codigoNorm
                && !daPropriaCadeia.Contains(a.Id)
                && _dbContext.VinculosAtoEntidade
                    .Any(v => v.AtoId == a.Id && v.EntidadeTipo == tipoNorm && v.EntidadeId == entidadeId))
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AtoNormativo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPorEntidadeAsync(
            string entidadeTipo,
            Guid entidadeId,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidadeTipo);

        string tipoNorm = entidadeTipo.Trim();

        // EXISTS sobre o vínculo, servido por ix_vinculo_ato_entidade_objeto — não um
        // JOIN, que duplicaria o ato vinculado à mesma entidade por mais de um caminho.
        IQueryable<AtoNormativo> query = _dbContext.AtosNormativos
            .AsNoTracking()
            .Where(a => _dbContext.VinculosAtoEntidade
                .Any(v => v.AtoId == a.Id && v.EntidadeTipo == tipoNorm && v.EntidadeId == entidadeId));

        KeysetOrdenadoPage<AtoNormativo> page = await KeysetOrdenadoCursor
            .ApplyAsync(
                query,
                b => b.Ascending(a => a.DataPublicacao).Ascending(a => a.Id),
                SortKeyDaData,
                (sortKey, id) => new { DataPublicacao = DataDaSortKey(sortKey), Id = id },
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Anterior, page.Proximo);
    }

    /// <summary>
    /// Sort key da âncora: a data de publicação em ISO-8601 (<c>yyyy-MM-dd</c>). Ordem
    /// lexicográfica e ordem cronológica coincidem nesse formato, e ele é estável entre
    /// culturas. Nunca nula (ADR-0095): <c>data_publicacao</c> é coluna obrigatória.
    /// </summary>
    private static string SortKeyDaData(AtoNormativo ato) =>
        ato.DataPublicacao.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly DataDaSortKey(string sortKey) =>
        DateOnly.ParseExact(sortKey, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
