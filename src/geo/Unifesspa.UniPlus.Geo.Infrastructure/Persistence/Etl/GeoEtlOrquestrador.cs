namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Geo.Infrastructure.Observability;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Orquestra a atualização periódica do ETL DNE (Story #674): regista o disparo,
/// garante uma carga por vez (índice único parcial → 409), executa os importadores de
/// topo (#672) e folhas (#673) em segundo plano, aplica a política de stale na recarga,
/// sela a versão vigente do cache de CEP e persiste status + relatório (sem PII). É
/// serviço transacional de Infrastructure — não um command Wolverine (ADR-0092).
/// </summary>
internal sealed partial class GeoEtlOrquestrador : IGeoImportacaoService, IGeoImportacaoExecutor
{
    private const string PostgresUniqueViolation = "23505";

    private readonly GeoDbContext _contexto;
    private readonly IGeoImportador _importadorTopo;
    private readonly IGeoImportadorLocalidades _importadorFolhas;
    private readonly IGeoFonteDadosFactory _fonteFactory;
    private readonly IGeoCepCacheInvalidador _cacheInvalidador;
    private readonly IGeoImportacaoFila _fila;
    private readonly GeoEtlMetrics _metricas;
    private readonly TimeProvider _relogio;
    private readonly ILogger<GeoEtlOrquestrador> _logger;

    public GeoEtlOrquestrador(
        GeoDbContext contexto,
        IGeoImportador importadorTopo,
        IGeoImportadorLocalidades importadorFolhas,
        IGeoFonteDadosFactory fonteFactory,
        IGeoCepCacheInvalidador cacheInvalidador,
        IGeoImportacaoFila fila,
        GeoEtlMetrics metricas,
        TimeProvider relogio,
        ILogger<GeoEtlOrquestrador> logger)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(importadorTopo);
        ArgumentNullException.ThrowIfNull(importadorFolhas);
        ArgumentNullException.ThrowIfNull(fonteFactory);
        ArgumentNullException.ThrowIfNull(cacheInvalidador);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(metricas);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        _contexto = contexto;
        _importadorTopo = importadorTopo;
        _importadorFolhas = importadorFolhas;
        _fonteFactory = fonteFactory;
        _cacheInvalidador = cacheInvalidador;
        _fila = fila;
        _metricas = metricas;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<Result<Guid>> IniciarAsync(string versao, string disparadoPor, CancellationToken cancellationToken)
    {
        Result<GeoImportacaoExecucao> criacao =
            GeoImportacaoExecucao.Iniciar(versao, disparadoPor, _relogio.GetUtcNow());
        if (criacao.IsFailure)
        {
            return Result<Guid>.Failure(criacao.Error!);
        }

        GeoImportacaoExecucao execucao = criacao.Value!;
        _contexto.Set<GeoImportacaoExecucao>().Add(execucao);

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (EhViolacaoUnica(ex))
        {
            // Índice único parcial (status = EmAndamento): já há uma carga em andamento.
            // Mesmo tratamento do EfCoreIdempotencyStore — só 23505 vira conflito; demais
            // DbUpdateException propagam como 5xx.
            _contexto.Set<GeoImportacaoExecucao>().Entry(execucao).State = EntityState.Detached;
            LogConflito(_logger, execucao.VersaoDataset);
            return Result<Guid>.Failure(new DomainError(
                GeoImportacaoErrorCodes.ImportacaoEmAndamento,
                "Já existe uma importação do Geo em andamento."));
        }

        // Enfileira com CancellationToken.None: a linha já está persistida, então cancelar
        // o request não pode deixar a execução órfã (registrada mas sem item na fila). Se o
        // canal estiver fechado (serviço desligando), marca a execução como falha.
        try
        {
            await _fila.EnfileirarAsync(execucao.Id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            await MarcarFalhaAsync(execucao, "Serviço em desligamento; carga não enfileirada.", relatorioJson: null, CancellationToken.None).ConfigureAwait(false);
            LogNaoEnfileirada(_logger, execucao.Id, ex);
            return Result<Guid>.Failure(new DomainError(
                GeoImportacaoErrorCodes.NaoEnfileirada,
                "Não foi possível enfileirar a importação (serviço em desligamento)."));
        }

        // O subject do disparador fica no registro auditável (disparado_por), não no log
        // estruturado — minimiza identificadores pessoais/operacionais em logs (CA-08).
        LogDisparada(_logger, execucao.Id, execucao.VersaoDataset);
        return Result<Guid>.Success(execucao.Id);
    }

    public async Task<ImportacaoGeoDto?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        GeoImportacaoExecucao? execucao = await _contexto.Set<GeoImportacaoExecucao>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return execucao is null ? null : MapearDto(execucao);
    }

    public async Task ExecutarAsync(Guid execucaoId, CancellationToken cancellationToken)
    {
        GeoImportacaoExecucao? execucao = await _contexto.Set<GeoImportacaoExecucao>()
            .FirstOrDefaultAsync(e => e.Id == execucaoId, cancellationToken)
            .ConfigureAwait(false);

        if (execucao is null || execucao.Status != StatusImportacao.EmAndamento)
        {
            LogExecucaoIgnorada(_logger, execucaoId);
            return;
        }

        string versao = execucao.VersaoDataset;
        long inicio = _relogio.GetTimestamp();

        try
        {
            bool baseVazia = !await _contexto.Cidades.AnyAsync(cancellationToken).ConfigureAwait(false);
            ModoCarga modo = baseVazia ? ModoCarga.Inicial : ModoCarga.Recarga;

            IGeoFonteDados fonte = _fonteFactory.Criar(versao);

            RelatorioImportacao relTopo = await _importadorTopo.ImportarAsync(fonte, cancellationToken).ConfigureAwait(false);
            RelatorioImportacao relFolhas = await _importadorFolhas.ImportarAsync(fonte, modo, cancellationToken).ConfigureAwait(false);

            // Política de stale: só na recarga (a inicial parte de base vazia). Marca
            // vigente=false nas linhas não revistas nesta versão (versao_dataset anterior).
            if (modo == ModoCarga.Recarga)
            {
                await GeoStaleMarker.MarcarStaleAsync(_contexto, versao, _relogio.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            }

            RelatorioImportacaoDto relatorio = CombinarRelatorios(versao, relTopo, relFolhas);
            string relatorioJson = JsonSerializer.Serialize(relatorio);
            double duracaoMs = _relogio.GetElapsedTime(inicio).TotalMilliseconds;

            Result conclusao = execucao.Concluir(_relogio.GetUtcNow(), relatorioJson, ResumoConclusao(relatorio, modo));
            if (conclusao.IsFailure)
            {
                LogTransicaoInesperada(_logger, execucaoId, conclusao.Error!.Code);
            }

            await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Só após a conclusão estar persistida: sela a versão vigente do cache de CEP
            // (best-effort). Se o SaveChanges acima falhasse, o cache não apontaria para uma
            // versão cuja execução não foi confirmada como Concluída.
            await _cacheInvalidador.InvalidarAsync(versao, cancellationToken).ConfigureAwait(false);

            _metricas.RegistrarConclusao(versao, duracaoMs, relatorio.Inseridos + relatorio.Atualizados, relatorio.Degradados);
            LogConcluida(_logger, execucaoId, versao, relatorio.Inseridos, relatorio.Atualizados, relatorio.Degradados);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            double duracaoMs = _relogio.GetElapsedTime(inicio).TotalMilliseconds;
            // Mensagem genérica persistida (sem detalhe de infra/PII); a exceção vai ao log.
            await MarcarFalhaAsync(execucao, "Falha na carga do ETL DNE.", relatorioJson: null, CancellationToken.None).ConfigureAwait(false);
            _metricas.RegistrarFalha(versao, duracaoMs);
            LogFalhou(_logger, execucaoId, versao, excecao);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Persistir o estado de falha é best-effort à beira do worker; um erro aqui não pode escalar (a carga já terminou).")]
    private async Task MarcarFalhaAsync(GeoImportacaoExecucao execucao, string mensagem, string? relatorioJson, CancellationToken cancellationToken)
    {
        // Falhar é idempotente: se a execução já saiu de EmAndamento, retorna failure e nada muda.
        execucao.Falhar(_relogio.GetUtcNow(), mensagem, relatorioJson);
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaPersistir(_logger, execucao.Id, excecao);
        }
    }

    private static bool EhViolacaoUnica(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolation;

    private static ImportacaoGeoDto MapearDto(GeoImportacaoExecucao execucao)
    {
        RelatorioImportacaoDto? relatorio = execucao.RelatorioJson is null
            ? null
            : JsonSerializer.Deserialize<RelatorioImportacaoDto>(execucao.RelatorioJson);

        return new ImportacaoGeoDto(
            execucao.Id,
            execucao.VersaoDataset,
            execucao.Status.ToString(),
            execucao.IniciadoEm,
            execucao.ConcluidoEm,
            execucao.DisparadoPor,
            execucao.Mensagem,
            relatorio);
    }

    private static string ResumoConclusao(RelatorioImportacaoDto relatorio, ModoCarga modo) =>
        $"Modo {modo}: {relatorio.Inseridos} inseridos, {relatorio.Atualizados} atualizados, " +
        $"{relatorio.Orfaos} órfãos, {relatorio.Degradados} degradados.";

    private static RelatorioImportacaoDto CombinarRelatorios(string versao, params RelatorioImportacao[] relatorios)
    {
        Dictionary<string, TabelaImportacaoDto> mapa = new(StringComparer.Ordinal);
        foreach (RelatorioImportacao relatorio in relatorios)
        {
            foreach ((string nome, ContadorTabela contador) in relatorio.Tabelas)
            {
                mapa[nome] = mapa.TryGetValue(nome, out TabelaImportacaoDto? existente)
                    ? Somar(existente, contador)
                    : ParaDto(nome, contador);
            }
        }

        List<TabelaImportacaoDto> tabelas = [.. mapa.Values.OrderBy(t => t.Tabela, StringComparer.Ordinal)];
        return new RelatorioImportacaoDto(
            versao,
            tabelas.Sum(t => t.Lidos),
            tabelas.Sum(t => t.Inseridos),
            tabelas.Sum(t => t.Atualizados),
            tabelas.Sum(t => t.Orfaos),
            tabelas.Sum(t => t.ParsesDegradados),
            tabelas);
    }

    private static TabelaImportacaoDto ParaDto(string nome, ContadorTabela c) =>
        new(nome, c.Lidos, c.Inseridos, c.Atualizados, c.IgnoradosSemChave, c.Orfaos, c.Duplicados, c.ParsesDegradados, [.. c.Amostras]);

    private static TabelaImportacaoDto Somar(TabelaImportacaoDto a, ContadorTabela c)
    {
        const int MaxAmostras = 25;
        List<string> amostras = [.. a.Amostras];
        foreach (string amostra in c.Amostras)
        {
            if (amostras.Count >= MaxAmostras)
            {
                break;
            }

            amostras.Add(amostra);
        }

        return new TabelaImportacaoDto(
            a.Tabela,
            a.Lidos + c.Lidos,
            a.Inseridos + c.Inseridos,
            a.Atualizados + c.Atualizados,
            a.IgnoradosSemChave + c.IgnoradosSemChave,
            a.Orfaos + c.Orfaos,
            a.Duplicados + c.Duplicados,
            a.ParsesDegradados + c.ParsesDegradados,
            amostras);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: carga {ExecucaoId} disparada (versão {Versao}).")]
    private static partial void LogDisparada(ILogger logger, Guid execucaoId, string versao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: disparo recusado (versão {Versao}) — já há uma carga em andamento.")]
    private static partial void LogConflito(ILogger logger, string versao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: execução {ExecucaoId} não enfileirada (serviço em desligamento).")]
    private static partial void LogNaoEnfileirada(ILogger logger, Guid execucaoId, Exception excecao);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: execução {ExecucaoId} ignorada (inexistente ou já finalizada).")]
    private static partial void LogExecucaoIgnorada(ILogger logger, Guid execucaoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: carga {ExecucaoId} concluída (versão {Versao}, inseridos={Inseridos}, atualizados={Atualizados}, degradados={Degradados}).")]
    private static partial void LogConcluida(ILogger logger, Guid execucaoId, string versao, int inseridos, int atualizados, int degradados);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: carga {ExecucaoId} falhou (versão {Versao}).")]
    private static partial void LogFalhou(ILogger logger, Guid execucaoId, string versao, Exception excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: transição inesperada na execução {ExecucaoId} ({Codigo}).")]
    private static partial void LogTransicaoInesperada(ILogger logger, Guid execucaoId, string codigo);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao persistir o estado de falha da execução {ExecucaoId}.")]
    private static partial void LogFalhaPersistir(ILogger logger, Guid execucaoId, Exception excecao);
}
