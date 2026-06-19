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
/// serviço de Infrastructure — não um command Wolverine (ADR-0092).
/// </summary>
/// <remarks>
/// O <strong>registro</strong> de execução é gravado de forma transacional, mas a
/// <strong>carga</strong> não é atômica entre fases: topo, folhas (COPY com transação
/// própria), os UPDATEs de stale (autocommit por tabela) e a conclusão commitam em
/// separado. Um crash no meio deixa o dataset em estado parcial (versões mistas) — isso é
/// <strong>tolerado por design</strong>: a carga é idempotente por reaplicação (upsert por
/// chave natural), e a guarda de versão progressiva força reaplicar a mesma versão (ou mais
/// nova) para reconciliar. Transação única sobre milhões de linhas seria impraticável.
/// </remarks>
internal sealed partial class GeoEtlOrquestrador : IGeoImportacaoService, IGeoImportacaoExecutor
{
    private const string PostgresUniqueViolation = "23505";

    // Amostras de divergência já vêm limitadas da fonte (ContadorTabela); o cap aqui mantém
    // o limite explícito e consistente ao montar (ParaDto) e mesclar (Somar) o DTO.
    private const int MaxAmostrasPorTabela = 25;

    // Formato do relatorio_json persistido: camelCase, alinhado ao wire format da API
    // (ConfigureHttpJsonOptions) — round-trip consistente e jsonb consultável no mesmo padrão.
    private static readonly JsonSerializerOptions RelatorioJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly GeoDbContext _contexto;
    private readonly IGeoImportador _importadorTopo;
    private readonly IGeoImportadorLocalidades _importadorFolhas;
    private readonly IGeoFonteDadosFactory _fonteFactory;

    // Lazy de propósito: o invalidador resolve a cadeia Redis (IConnectionMultiplexer
    // conecta na construção). Só o ExecutarAsync (worker) usa o cache; manter Lazy evita
    // que o disparo/consulta (IniciarAsync/ObterAsync, caminho do endpoint) dependam do
    // Redis estar de pé — a borda continua respondendo 202/409/404 mesmo com cache fora.
    private readonly Lazy<IGeoCepCacheInvalidador> _cacheInvalidador;
    private readonly IGeoImportacaoFila _fila;
    private readonly GeoEtlMetrics _metricas;
    private readonly TimeProvider _relogio;
    private readonly ILogger<GeoEtlOrquestrador> _logger;

    public GeoEtlOrquestrador(
        GeoDbContext contexto,
        IGeoImportador importadorTopo,
        IGeoImportadorLocalidades importadorFolhas,
        IGeoFonteDadosFactory fonteFactory,
        Lazy<IGeoCepCacheInvalidador> cacheInvalidador,
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

        // Guarda de versão progressiva: recusa aplicar uma release anterior à última
        // concluída — aplicá-la rebaixaria o versao_dataset das linhas presentes e misturaria
        // datasets (a política de stale pressupõe versões não-decrescentes). Reaplicar a mesma
        // versão é permitido (idempotente). Comparação ordinal == numérica para AAAAMM fixo.
        // Guarda de versão progressiva: recusa aplicar uma release anterior à mais nova já
        // presente. O baseline vem de duas fontes para cobrir todos os casos:
        //  - os próprios dados (max(versao_dataset) das cidades): cobre um deploy novo sobre
        //    uma base já carregada (#672/#673) sem histórico de execução, e dados parciais de
        //    uma carga que falhou após commitar (as folhas commitam em fases);
        //  - o histórico de execuções terminais: cobre uma falha cujo topo deu rollback (sem
        //    dado novo persistido), mas cuja versão já foi atentada.
        // Reaplicar a mesma versão é permitido (idempotente). Comparação ordinal == numérica
        // para AAAAMM de comprimento fixo.
        string? versaoDados = await _contexto.Cidades
            .MaxAsync(c => (string?)c.VersaoDataset, cancellationToken)
            .ConfigureAwait(false);
        string? versaoHistorico = await _contexto.Set<GeoImportacaoExecucao>()
            .Where(e => e.Status == StatusImportacao.Concluida || e.Status == StatusImportacao.Falhou)
            .MaxAsync(e => (string?)e.VersaoDataset, cancellationToken)
            .ConfigureAwait(false);
        string? ultimaAplicada = MaiorVersao(versaoDados, versaoHistorico);
        if (ultimaAplicada is not null && string.CompareOrdinal(execucao.VersaoDataset, ultimaAplicada) < 0)
        {
            LogVersaoNaoProgressiva(_logger, execucao.VersaoDataset, ultimaAplicada);
            return Result<Guid>.Failure(new DomainError(
                GeoImportacaoErrorCodes.VersaoNaoProgressiva,
                $"A versão {execucao.VersaoDataset} é anterior à última aplicada ({ultimaAplicada})."));
        }

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
            await MarcarFalhaAsync(execucao, "Serviço em desligamento; carga não enfileirada.", CancellationToken.None).ConfigureAwait(false);
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
        // Lookup inicial com None: um cancelamento de shutdown aqui não pode escapar antes do
        // try (que marca a execução Falhou) — senão a linha ficaria EmAndamento bloqueando
        // disparos até a reconciliação por idade. O cancelamento passa a ser observado no
        // AnyAsync/importadores dentro do try.
        GeoImportacaoExecucao? execucao = await _contexto.Set<GeoImportacaoExecucao>()
            .FirstOrDefaultAsync(e => e.Id == execucaoId, CancellationToken.None)
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

            // Guarda de release vazia/sem âncora — ANTES das folhas: se a topo não persistiu
            // nenhuma cidade (staging não restaurado, sem o país-âncora BRA, ou sem cidades),
            // abortar já. Rodar as folhas marcaria logradouros/complementos com a nova versão
            // (resolvendo cidade_id pelas cidades existentes) e a recarga marcaria TODA a base
            // existente como vigente=false, selando uma versão vazia. A topo é transação única;
            // com zero cidades ela não publicou release, então a base permanece intacta.
            ContadorTabela cidade = relTopo.Tabela("cidade");
            if (cidade.Inseridos + cidade.Atualizados == 0)
            {
                await MarcarFalhaAsync(execucao, "Release vazia ou sem âncora (BRA/cidades) — carga recusada.", CancellationToken.None).ConfigureAwait(false);
                _metricas.RegistrarFalha(versao, _relogio.GetElapsedTime(inicio).TotalMilliseconds);
                LogReleaseVazia(_logger, execucaoId, versao);
                return;
            }

            RelatorioImportacao relFolhas = await _importadorFolhas.ImportarAsync(fonte, modo, cancellationToken).ConfigureAwait(false);

            // Guarda de folhas vazias — antes do stale: se nenhum logradouro foi aplicado, o
            // dump das tabelas-folha (CEP/logradouro) está incompleto. Numa recarga, o stale
            // marcaria TODOS os logradouros existentes vigente=false, quebrando o lookup de CEP
            // (o coração do módulo) até um rerun. DNE sempre traz logradouros; zero = dump
            // corrompido — falha antes de qualquer stale/conclusão.
            ContadorTabela logradouro = relFolhas.Tabela("logradouro");
            if (logradouro.Inseridos + logradouro.Atualizados == 0)
            {
                await MarcarFalhaAsync(execucao, "Tabelas-folha (logradouro) vazias — carga recusada.", CancellationToken.None).ConfigureAwait(false);
                _metricas.RegistrarFalha(versao, _relogio.GetElapsedTime(inicio).TotalMilliseconds);
                LogFolhasVazias(_logger, execucaoId, versao);
                return;
            }

            // A partir daqui os importadores já commitaram os dados — a finalização (stale +
            // conclusão + selo) roda com CancellationToken.None para deixar um estado terminal
            // consistente: um cancelamento de shutdown não pode gravar Concluída sem selar o
            // cache nem interromper a marcação de stale no meio.

            // Política de stale: só na recarga (a inicial parte de base vazia). Marca
            // vigente=false nas linhas não revistas nesta versão (versao_dataset anterior).
            if (modo == ModoCarga.Recarga)
            {
                await GeoStaleMarker.MarcarStaleAsync(_contexto, versao, _relogio.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
            }

            RelatorioImportacaoDto relatorio = CombinarRelatorios(versao, relTopo, relFolhas);
            string relatorioJson = JsonSerializer.Serialize(relatorio, RelatorioJsonOptions);
            double duracaoMs = _relogio.GetElapsedTime(inicio).TotalMilliseconds;

            Result conclusao = execucao.Concluir(_relogio.GetUtcNow(), relatorioJson, ResumoConclusao(relatorio, modo));
            if (conclusao.IsFailure)
            {
                LogTransicaoInesperada(_logger, execucaoId, conclusao.Error!.Code);
            }

            await _contexto.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

            // Só após a conclusão estar persistida: sela a versão vigente do cache de CEP,
            // de forma totalmente best-effort (ver SelarCacheVigenteAsync).
            await SelarCacheVigenteAsync(versao).ConfigureAwait(false);

            _metricas.RegistrarConclusao(versao, duracaoMs, relatorio.Inseridos + relatorio.Atualizados, relatorio.Degradados);
            LogConcluida(_logger, execucaoId, versao, relatorio.Inseridos, relatorio.Atualizados, relatorio.Degradados);
        }
        catch (OperationCanceledException)
        {
            // Cancelamento/desligamento durante a carga: esta instância é dona desta execução
            // e sabe que foi interrompida — marca-a terminal já (libera o índice único parcial),
            // em vez de deixá-la EmAndamento bloqueando disparos até a reconciliação por idade.
            // Persiste com None (o token de operação já está cancelado) e repropaga para o worker
            // tratar o shutdown.
            await MarcarFalhaAsync(execucao, "Carga interrompida (cancelamento/desligamento).", CancellationToken.None).ConfigureAwait(false);
            _metricas.RegistrarFalha(versao, _relogio.GetElapsedTime(inicio).TotalMilliseconds);
            LogCancelada(_logger, execucaoId, versao);
            throw;
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            double duracaoMs = _relogio.GetElapsedTime(inicio).TotalMilliseconds;
            // Mensagem genérica persistida (sem detalhe de infra/PII); a exceção vai ao log.
            await MarcarFalhaAsync(execucao, "Falha na carga do ETL DNE.", CancellationToken.None).ConfigureAwait(false);
            _metricas.RegistrarFalha(versao, duracaoMs);
            LogFalhou(_logger, execucaoId, versao, excecao);
        }
    }

    // Drenagem de desligamento (#694): marca Falhou uma execução enfileirada e não
    // processada. Reusa o ExecuteUpdate idempotente filtrado por status = EmAndamento — só
    // afeta a linha se ela ainda não terminou, liberando o índice único parcial.
    public Task MarcarInterrompidaNoDesligamentoAsync(Guid execucaoId, CancellationToken cancellationToken) =>
        MarcarFalhaPorIdAsync(execucaoId, "Carga interrompida no desligamento do worker.", cancellationToken);

    private Task MarcarFalhaAsync(GeoImportacaoExecucao execucao, string mensagem, CancellationToken cancellationToken) =>
        MarcarFalhaPorIdAsync(execucao.Id, mensagem, cancellationToken);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Persistir o estado de falha é best-effort à beira do worker; um erro aqui não pode escalar (a carga já terminou).")]
    private async Task MarcarFalhaPorIdAsync(Guid execucaoId, string mensagem, CancellationToken cancellationToken)
    {
        // ExecuteUpdate direto, filtrando status = EmAndamento no banco — NÃO muta/flusha a
        // entidade rastreada. Crítico quando o commit da conclusão falhou: a entidade está
        // Concluída in-memory, mas a linha continua EmAndamento no banco; um SaveChanges aqui
        // persistiria Concluída sem selar o cache. O filtro pelo estado real evita isso e é
        // idempotente (0 linhas se a execução já não está EmAndamento).
        DateTimeOffset agora = _relogio.GetUtcNow();
        try
        {
            await _contexto.Set<GeoImportacaoExecucao>()
                .Where(e => e.Id == execucaoId && e.Status == StatusImportacao.EmAndamento)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(e => e.Status, StatusImportacao.Falhou)
                        .SetProperty(e => e.ConcluidoEm, agora)
                        .SetProperty(e => e.Mensagem, mensagem)
                        .SetProperty(e => e.UpdatedAt, agora),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaPersistir(_logger, execucaoId, excecao);
        }
    }

    // Selo do cache totalmente best-effort: o .Value resolve a cadeia Redis (o Connect do
    // IConnectionMultiplexer pode lançar se o Redis estiver fora) FORA do catch interno do
    // invalidador, então o try aqui também o cobre. A execução já está Concluída no banco —
    // uma falha de cache/Redis não pode reverter o sucesso nem marcar falsa falha. Usa None:
    // a conclusão já está commitada e o selo não deve ser abortado por cancelamento.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Selo de cache best-effort após carga concluída: Redis/cache fora (inclusive no Connect resolvido pelo Lazy) não pode escalar para o caminho de falha.")]
    private async Task SelarCacheVigenteAsync(string versao)
    {
        try
        {
            await _cacheInvalidador.Value.InvalidarAsync(versao, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogFalhaSeloCache(_logger, versao, excecao);
        }
    }

    // Maior das duas versões AAAAMM (ordinal == numérica), tolerando nulos.
    private static string? MaiorVersao(string? a, string? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return string.CompareOrdinal(a, b) >= 0 ? a : b;
    }

    private static bool EhViolacaoUnica(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolation;

    private static ImportacaoGeoDto MapearDto(GeoImportacaoExecucao execucao)
    {
        RelatorioImportacaoDto? relatorio = execucao.RelatorioJson is null
            ? null
            : JsonSerializer.Deserialize<RelatorioImportacaoDto>(execucao.RelatorioJson, RelatorioJsonOptions);

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
        new(nome, c.Lidos, c.Inseridos, c.Atualizados, c.IgnoradosSemChave, c.Orfaos, c.Duplicados, c.ParsesDegradados,
            [.. c.Amostras.Take(MaxAmostrasPorTabela)]);

    private static TabelaImportacaoDto Somar(TabelaImportacaoDto a, ContadorTabela c)
    {
        List<string> amostras = [.. a.Amostras];
        foreach (string amostra in c.Amostras)
        {
            if (amostras.Count >= MaxAmostrasPorTabela)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: carga {ExecucaoId} interrompida por cancelamento/desligamento (versão {Versao}).")]
    private static partial void LogCancelada(ILogger logger, Guid execucaoId, string versao);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: carga {ExecucaoId} recusada — release {Versao} vazia ou sem âncora (nenhuma cidade aplicada).")]
    private static partial void LogReleaseVazia(ILogger logger, Guid execucaoId, string versao);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: carga {ExecucaoId} recusada — release {Versao} com tabelas-folha vazias (nenhum logradouro aplicado).")]
    private static partial void LogFolhasVazias(ILogger logger, Guid execucaoId, string versao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: falha ao selar a versão vigente {Versao} no cache (best-effort; carga já concluída).")]
    private static partial void LogFalhaSeloCache(ILogger logger, string versao, Exception excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: disparo recusado — versão {Versao} anterior à última aplicada {UltimaVersao}.")]
    private static partial void LogVersaoNaoProgressiva(ILogger logger, string versao, string ultimaVersao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: transição inesperada na execução {ExecucaoId} ({Codigo}).")]
    private static partial void LogTransicaoInesperada(ILogger logger, Guid execucaoId, string codigo);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao persistir o estado de falha da execução {ExecucaoId}.")]
    private static partial void LogFalhaPersistir(ILogger logger, Guid execucaoId, Exception excecao);
}
