namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class ModalidadeRepository : IModalidadeRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ModalidadeRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Modalidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Modalidades
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<Modalidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Modalidades
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Modalidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<Modalidade> page = await CursorKeyset
            .ApplyAsync(_dbContext.Modalidades.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(Modalidade modalidade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        await _dbContext.Modalidades.AddAsync(modalidade, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Modalidade modalidade)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        _dbContext.Modalidades.Remove(modalidade);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato nunca tem modalidade viva — evita query
        // desnecessária e garante que a comparação use o valor canônico normalizado
        // (Trim). Case-sensitive (default do Postgres) — alinhado ao índice único.
        Result<CodigoModalidade> codigoResult = CodigoModalidade.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        CodigoModalidade codigoVo = codigoResult.Value!;

        return _dbContext.Modalidades
            .AsNoTracking()
            .Where(m => excluirId == null || m.Id != excluirId)
            .AnyAsync(m => m.Codigo == codigoVo, cancellationToken);
    }

    public async Task<bool> CodigosVivosExistemAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        var normalizados = codigos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizados.Count == 0)
        {
            return true;
        }

        // Qualquer código fora do formato canônico não pode ter modalidade viva —
        // logo "nem todos existem".
        var codigosVo = new List<CodigoModalidade>(normalizados.Count);
        foreach (string codigo in normalizados)
        {
            Result<CodigoModalidade> resultado = CodigoModalidade.Criar(codigo);
            if (resultado.IsFailure)
            {
                return false;
            }

            codigosVo.Add(resultado.Value!);
        }

        int encontrados = await _dbContext.Modalidades
            .AsNoTracking()
            .Where(m => codigosVo.Contains(m.Codigo))
            .Select(m => m.Codigo)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return encontrados == codigosVo.Count;
    }

    public Task<bool> EhReferenciadaPorOutraModalidadeVivaAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        string codigoNorm = codigo.Trim();

        // Consulta jsonb: composicao_origem OU qualquer campo de remanejamento_args
        // (destino/par/fallback, extraídos por ->>). SQL cru é necessário porque
        // RemanejamentoArgs é serializado como jsonb via value converter e o EF não
        // traduz acesso aos campos internos em LINQ. O query filter global de
        // soft-delete é aplicado por composição sobre o FromSql.
        FormattableString sql = $@"
            SELECT * FROM configuracao.modalidade
            WHERE composicao_origem = {codigoNorm}
               OR remanejamento_args ->> 'destino' = {codigoNorm}
               OR remanejamento_args ->> 'par' = {codigoNorm}
               OR remanejamento_args ->> 'fallback' = {codigoNorm}";

        IQueryable<Modalidade> consulta = _dbContext.Modalidades
            .FromSqlInterpolated(sql)
            .AsNoTracking();

        if (excluirId is { } id)
        {
            consulta = consulta.Where(m => m.Id != id);
        }

        return consulta.AnyAsync(cancellationToken);
    }
}
