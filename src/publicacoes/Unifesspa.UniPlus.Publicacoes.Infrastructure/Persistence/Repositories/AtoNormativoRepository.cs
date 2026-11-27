namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;

using System.Linq;

using Microsoft.EntityFrameworkCore;


using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
public sealed class AtoNormativoRepository : IAtoNormativoRepository
{
    /// <summary>
    /// Sobe de <c>{0}</c> até a raiz da cadeia de retificação. O passo é fixo, mas a profundidade não —
    /// daí SQL recursivo, e não JOIN. A cadeia é linear (índice único parcial em <c>ato_retificado_id</c>)
    /// e acíclica (gatilho <c>fn_atos_normativos_cadeia_aciclica</c>); o <c>CYCLE</c> existe para que um
    /// ciclo que escape da escrita seja cortado na leitura, em vez de pendurar a conexão. É passada a
    /// <c>SqlQueryRaw</c> com o id como parâmetro: interpolada numa <c>FormattableString</c>, a própria
    /// constante viraria parâmetro.
    /// </summary>
    private const string AncestraisDoAto = """
        WITH RECURSIVE ancestrais AS (
            SELECT id, ato_retificado_id
            FROM publicacoes.atos_normativos
            WHERE id = {0}
            UNION ALL
            SELECT a.id, a.ato_retificado_id
            FROM publicacoes.atos_normativos a
            JOIN ancestrais an ON a.id = an.ato_retificado_id
        ) CYCLE id SET ciclo_acima USING caminho_acima
        """;

    private const string SqlDaRaizDaCadeia = AncestraisDoAto + """

        SELECT id AS "Value" FROM ancestrais WHERE ato_retificado_id IS NULL AND NOT ciclo_acima
        """;

    /// <summary>
    /// A raiz que <see cref="AncestraisDoAto"/> encontra, e dela a descida até a cabeça: a linhagem
    /// inteira, com o <c>CYCLE</c> também no sentido da descida.
    /// </summary>
    private const string SqlDaCadeiaInteira = AncestraisDoAto + """
        ,
        cadeia AS (
            SELECT id, ato_retificado_id
            FROM publicacoes.atos_normativos
            WHERE id IN (
                SELECT id FROM ancestrais WHERE ato_retificado_id IS NULL AND NOT ciclo_acima
            )
            UNION ALL
            SELECT a.id, a.ato_retificado_id
            FROM publicacoes.atos_normativos a
            JOIN cadeia c ON a.ato_retificado_id = c.id
        ) CYCLE id SET ciclo_abaixo USING caminho_abaixo
        SELECT id AS "Value" FROM cadeia WHERE NOT ciclo_abaixo
        """;

    private readonly UniPlusDbContext _dbContext;

    public AtoNormativoRepository(UniPlusDbContext dbContext)
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
            .Include(a => a.Anexos)
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
        return await _dbContext.Database
            .SqlQueryRaw<Guid>(SqlDaCadeiaInteira, atoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListarIdsComMesmaNumeracaoAsync(
        NumeracaoDoAto numeracao,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeracao.Orgao, nameof(numeracao));
        ArgumentException.ThrowIfNullOrWhiteSpace(numeracao.Serie, nameof(numeracao));
        ArgumentException.ThrowIfNullOrWhiteSpace(numeracao.Numero, nameof(numeracao));

        return await _dbContext.AtosNormativos
            .AsNoTracking()
            .Where(a => a.Orgao == numeracao.Orgao
                && a.Serie == numeracao.Serie
                && a.Ano == numeracao.Ano
                && a.Numero == numeracao.Numero)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<AtoNormativo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena por Id (Guid v7) e
        // fatia, devolvendo os pontos de continuação de prev/next.
        IQueryable<AtoNormativo> query = _dbContext.AtosNormativos.AsNoTracking().Include(a => a.Anexos);

        OrderedKeysetPage<AtoNormativo> page = await CursorKeyset
            .ApplyAsync(query, CondicoesDoFiltro.Nenhuma, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public async Task<Guid> ObterRaizDaCadeiaAsync(Guid atoId, CancellationToken cancellationToken)
    {
        List<Guid> raizes = await _dbContext.Database
            .SqlQueryRaw<Guid>(SqlDaRaizDaCadeia, atoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Zero raízes só acontece se o ato não existe — o handler já o validou antes.
        return raizes.Count == 1
            ? raizes[0]
            : throw new InvalidOperationException(
                $"A cadeia de retificação do ato {atoId} não tem raiz única — a linearidade foi violada no banco.");
    }

    public Task<VagaDeLinhagem?> ObterVagaDoObjetoAsync(
        EntidadeVinculada entidade,
        string tipoCodigo,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidade.Tipo, nameof(entidade));
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);


        return _dbContext.VagasDeLinhagem
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.EntidadeTipo == entidade.Tipo && l.EntidadeId == entidade.Id && l.TipoCodigo == tipoCodigo,
                cancellationToken);
    }

    public async Task ReservarVagaAsync(VagaDeLinhagem vaga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vaga);
        await _dbContext.VagasDeLinhagem.AddAsync(vaga, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EntidadeVinculada>> ListarVinculosDoAtoAsync(
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

        return [.. vinculos.Select(v => v.Entidade)];
    }

    public Task<AtoNormativo?> ObterAtoConflitanteNoObjetoAsync(
        EntidadeVinculada entidade,
        string tipoCodigo,
        IReadOnlyCollection<Guid> idsDaPropriaCadeia,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);
        ArgumentNullException.ThrowIfNull(idsDaPropriaCadeia);

        Guid[] daPropriaCadeia = [.. idsDaPropriaCadeia];

        return VinculadosA(entidade)
            .Where(a => a.TipoCodigo == tipoCodigo && !daPropriaCadeia.Contains(a.Id))
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AtoNormativo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPorEntidadeAsync(
            EntidadeVinculada entidade,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken)
    {
        OrderedKeysetPage<AtoNormativo> page = await OrderedKeysetCursor
            .ApplyAsync(
                VinculadosA(entidade).Include(a => a.Anexos), OrdemDaDivulgacao, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    // EXISTS sobre o vínculo, servido por ix_vinculos_do_ato_com_entidade_objeto — não um
    // JOIN, que duplicaria o ato vinculado à mesma entidade por mais de um caminho.
    private IQueryable<AtoNormativo> VinculadosA(EntidadeVinculada entidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidade.Tipo, nameof(entidade));

        return _dbContext.AtosNormativos
            .AsNoTracking()
            .Where(a => _dbContext.Set<VinculoAtoEntidade>()
                .Any(v => v.AtoId == a.Id && v.EntidadeTipo == entidade.Tipo && v.EntidadeId == entidade.Id));
    }

    // Nunca nula (ADR-0095): publicado_em é coluna obrigatória.
    private static readonly KeysetSort<AtoNormativo> OrdemDaDivulgacao = new(
        [KeysetSortColumn<AtoNormativo>.For("publicado_em", a => a.PublicadoEm, a => InstanteDoCursor.Escrever(a.PublicadoEm))],
        static (partes, id) => new { PublicadoEm = InstanteDoCursor.Ler(partes[0]), Id = id },
        CondicoesDoFiltro.Nenhuma);
}
