namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Bulk;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NetTopologySuite.Geometries;

using Npgsql;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Carga em lote (COPY binário) das folhas de alto volume: os complementos por CEP
/// (~235k) e os ~1,4M logradouros (ADR-0092, Story #673). Estratégia única para os dois
/// modos (<see cref="ModoCarga"/>): COPY streamado para uma <strong>tabela de staging
/// TEMP sem constraints</strong> → <c>INSERT ... SELECT DISTINCT ON (chave natural) ...
/// ON CONFLICT DO UPDATE</c> (dedup intra-fonte + idempotência) → drop do staging. Tudo
/// numa <strong>única conexão física</strong> (o COPY, o merge e os índices precisam
/// compartilhar a sessão; a fase de índices roda em autocommit, fora de transação).
/// </summary>
/// <remarks>
/// <para><strong>Índices pesados (logradouro):</strong> no modo <see cref="ModoCarga.Inicial"/>
/// a tabela é truncada e os índices <c>gin_trgm</c>/<c>GIST</c> são dropados antes do COPY
/// e recriados via <c>CREATE INDEX CONCURRENTLY</c> ao fim — <c>CONCURRENTLY</c> não pode
/// rodar dentro de transação, por isso a conexão fica em autocommit (sem
/// <c>BeginTransaction</c>). No modo <see cref="ModoCarga.Recarga"/> os índices permanecem.</para>
/// <para><strong>Auditoria sem interceptor:</strong> o COPY/merge ignoram o
/// <c>AuditableInterceptor</c>; o <c>created_at</c> é carimbado pelo <see cref="TimeProvider"/>
/// no insert e preservado no conflito; o <c>updated_at</c> é setado só no <c>DO UPDATE</c>.</para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Todo o SQL desta classe é literal de código (DDL de staging, COPY e merge constantes; os nomes de tabela alvo/staging vêm de uma lista fixa de literais). O único valor parametrizado (@now) é um NpgsqlParameter. Não há entrada de usuário — COPY/DDL não são parametrizáveis por identificador.")]
internal sealed partial class LogradouroCopyImporter
{
    // Lote do COPY para o staging: limita a memória por chamada e dá granularidade de
    // progresso. O staging acumula todos os lotes; o merge final é uma única instrução.
    private const int TamanhoLote = 50_000;

    private const string CopyComplemento =
        "COPY logradouro_complemento_staging (id, cep, complemento, complemento_normalizado, versao_dataset, created_at) FROM STDIN (FORMAT BINARY)";

    private const string CriarStagingComplemento = """
        DROP TABLE IF EXISTS logradouro_complemento_staging;
        CREATE TEMP TABLE logradouro_complemento_staging (
            id uuid, cep text, complemento text, complemento_normalizado text,
            versao_dataset text, created_at timestamptz, _ord bigserial);
        """;

    private const string MergeComplemento = """
        INSERT INTO logradouro_complemento
            (id, cep, complemento, complemento_normalizado, versao_dataset, vigente, created_at, updated_at)
        SELECT DISTINCT ON (cep, complemento_normalizado)
            id, cep, complemento, complemento_normalizado, versao_dataset, true, created_at, NULL
        FROM logradouro_complemento_staging
        ORDER BY cep, complemento_normalizado, _ord DESC
        ON CONFLICT (cep, complemento_normalizado) DO UPDATE SET
            complemento = EXCLUDED.complemento,
            versao_dataset = EXCLUDED.versao_dataset,
            vigente = true,
            updated_at = @now;
        """;

    private const string CopyLogradouro =
        "COPY logradouro_staging (id, cep, tipo, nome, nome_completo, nome_normalizado, cidade_id, distrito_id, bairro_id, uf, latitude, longitude, coordenada, ativo, versao_dataset, created_at) FROM STDIN (FORMAT BINARY)";

    private const string CriarStagingLogradouro = """
        DROP TABLE IF EXISTS logradouro_staging;
        CREATE TEMP TABLE logradouro_staging (
            id uuid, cep text, tipo text, nome text, nome_completo text, nome_normalizado text,
            cidade_id uuid, distrito_id uuid, bairro_id uuid, uf text,
            latitude numeric(9,6), longitude numeric(9,6), coordenada geography(Point,4326),
            ativo boolean, versao_dataset text, created_at timestamptz, _ord bigserial);
        """;

    private const string MergeLogradouro = """
        INSERT INTO logradouro
            (id, cep, tipo, nome, nome_completo, nome_normalizado, cidade_id, distrito_id, bairro_id,
             uf, latitude, longitude, coordenada, ativo, versao_dataset, vigente, created_at, updated_at)
        SELECT DISTINCT ON (cep, nome_normalizado, cidade_id)
            id, cep, tipo, nome, nome_completo, nome_normalizado, cidade_id, distrito_id, bairro_id,
            uf, latitude, longitude, coordenada, ativo, versao_dataset, true, created_at, NULL
        FROM logradouro_staging
        ORDER BY cep, nome_normalizado, cidade_id, _ord DESC
        ON CONFLICT (cep, nome_normalizado, cidade_id) DO UPDATE SET
            tipo = EXCLUDED.tipo, nome = EXCLUDED.nome, nome_completo = EXCLUDED.nome_completo,
            distrito_id = EXCLUDED.distrito_id, bairro_id = EXCLUDED.bairro_id, uf = EXCLUDED.uf,
            latitude = EXCLUDED.latitude, longitude = EXCLUDED.longitude, coordenada = EXCLUDED.coordenada,
            ativo = EXCLUDED.ativo, versao_dataset = EXCLUDED.versao_dataset,
            vigente = true, updated_at = @now;
        """;

    // Mesma chave de connection string usada por GeoInfrastructureRegistration para o
    // GeoDbContext. Lida da IConfiguration (não redatada) — o getter do NpgsqlConnection
    // remove a senha após a 1ª abertura (Persist Security Info=false), então não dá para
    // reaproveitar a string do DbContext já usado.
    private const string ConnectionStringName = "GeoDb";

    private readonly string _connectionString;
    private readonly TimeProvider _clock;
    private readonly ILogger<LogradouroCopyImporter> _logger;

    public LogradouroCopyImporter(IConfiguration configuration, TimeProvider clock, ILogger<LogradouroCopyImporter> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} não configurada para o COPY do ETL Geo.");
        _clock = clock;
        _logger = logger;
    }

    public async Task ImportarAsync(
        IGeoFonteDados fonte,
        ModoCarga modo,
        ResolucaoLocalidades resolucao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        ArgumentNullException.ThrowIfNull(resolucao);
        ArgumentNullException.ThrowIfNull(relatorio);

        // Datasource dedicado com NTS em modo geography-default: o COPY binário escreve
        // o Point como geography(Point,4326), batendo com a coluna. Sem isso o handler
        // default (geometry) produziria EWKB incompatível com a coluna geography.
        NpgsqlDataSourceBuilder builder = new(_connectionString);
        builder.UseNetTopologySuite(geographyAsDefault: true);
        NpgsqlDataSource dataSource = builder.Build();
        await using (dataSource.ConfigureAwait(false))
        {
            NpgsqlConnection conexao = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (conexao.ConfigureAwait(false))
            {
                // Um único instante para toda a carga (created_at dos inserts, updated_at
                // dos conflitos) — consistente e auditável.
                DateTimeOffset agora = _clock.GetUtcNow();

                await ImportarComplementosAsync(conexao, fonte, agora, relatorio, cancellationToken).ConfigureAwait(false);
                await ImportarLogradourosAsync(conexao, fonte, modo, resolucao, agora, relatorio, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ImportarComplementosAsync(
        NpgsqlConnection conexao,
        IGeoFonteDados fonte,
        DateTimeOffset agora,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("logradouro_complemento");
        await ExecutarAsync(conexao, CriarStagingComplemento, cancellationToken).ConfigureAwait(false);

        long staged = await CopiarEmLotesAsync(
            conexao,
            CopyComplemento,
            ProjetarComplementosAsync(fonte, contador, cancellationToken),
            EscreverComplementoAsync,
            agora,
            "logradouro_complemento",
            cancellationToken).ConfigureAwait(false);

        await MergearContandoAsync(conexao, contador, "logradouro_complemento", MergeComplemento, staged, agora, cancellationToken).ConfigureAwait(false);
        await ExecutarAsync(conexao, "DROP TABLE IF EXISTS logradouro_complemento_staging;", cancellationToken).ConfigureAwait(false);
    }

    private async Task ImportarLogradourosAsync(
        NpgsqlConnection conexao,
        IGeoFonteDados fonte,
        ModoCarga modo,
        ResolucaoLocalidades resolucao,
        DateTimeOffset agora,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("logradouro");

        // Modo Inicial: base limpa para COPY rápido. TRUNCATE torna a reexecução do
        // Inicial idempotente (sem violar a UNIQUE por estado parcial anterior). Fica
        // fora do try: se falhar, nenhum índice foi dropado ainda, nada a recuperar.
        if (modo == ModoCarga.Inicial)
        {
            await ExecutarAsync(conexao, "TRUNCATE TABLE logradouro;", cancellationToken).ConfigureAwait(false);
        }

        bool concluido = false;
        try
        {
            // Drop dos índices pesados e criação do staging DENTRO do try: como rodam em
            // autocommit, um drop já é permanente; qualquer falha a partir daqui (entre os
            // dois drops, na criação do staging, no COPY/merge) cai no finally que recria.
            if (modo == ModoCarga.Inicial)
            {
                await DroparIndicesPesadosAsync(conexao, cancellationToken).ConfigureAwait(false);
            }

            await ExecutarAsync(conexao, CriarStagingLogradouro, cancellationToken).ConfigureAwait(false);

            long staged = await CopiarEmLotesAsync(
                conexao,
                CopyLogradouro,
                ProjetarLogradourosAsync(fonte, resolucao, contador, cancellationToken),
                EscreverLogradouroAsync,
                agora,
                "logradouro",
                cancellationToken).ConfigureAwait(false);

            await MergearContandoAsync(conexao, contador, "logradouro", MergeLogradouro, staged, agora, cancellationToken).ConfigureAwait(false);
            await ExecutarAsync(conexao, "DROP TABLE IF EXISTS logradouro_staging;", cancellationToken).ConfigureAwait(false);
            concluido = true;
        }
        finally
        {
            // No modo Inicial os índices pesados são dropados dentro do try. Recria-os
            // sempre — mesmo em falha/cancelamento do bulk — para a tabela nunca ficar sem
            // gin_trgm/GIST (degradaria o lookup/autocomplete até um rerun). Em ambos os
            // caminhos a recriação roda com CancellationToken.None: cada índice é dropado
            // logo antes de ser recriado, então interromper no meio deixaria a tabela sem
            // um índice pesado — concluir é o estado correto. No sucesso os erros propagam;
            // na falha são best-effort (sem mascarar a exceção original do bulk). Rerun do
            // Inicial continua sendo a recuperação canônica.
            if (modo == ModoCarga.Inicial)
            {
                if (concluido)
                {
                    await RecriarIndicesPesadosAsync(conexao, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await RecriarIndicesPesadosBestEffortAsync(conexao).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task RecriarIndicesPesadosBestEffortAsync(NpgsqlConnection conexao)
    {
        try
        {
            // CancellationToken.None: a recuperação roda mesmo se o cancelamento foi a causa
            // da falha — caso contrário a tabela ficaria sem índices.
            await RecriarIndicesPesadosAsync(conexao, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            // Não mascara a exceção original do bulk; o rerun do Inicial recria os índices.
            LogRecriacaoIndicesFalhou(_logger, ex);
        }
    }

    // Projeção streamada: valida/normaliza via factory do domínio (fonte única de
    // normalização), conta lido/ignorado e emite a entidade pronta para o COPY.
    private static async IAsyncEnumerable<LogradouroComplemento> ProjetarComplementosAsync(
        IGeoFonteDados fonte,
        ContadorTabela contador,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (LogradouroComplementoCru cru in fonte.LerLogradouroComplementosAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            Result<LogradouroComplemento> resultado = LogradouroComplemento.Importar(
                cru.Cep ?? string.Empty,
                cru.Complemento ?? string.Empty,
                cru.ComplementoSemAcento ?? cru.Complemento ?? string.Empty,
                fonte.Versao);

            if (resultado.IsFailure)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            yield return resultado.Value!;
        }
    }

    private static async IAsyncEnumerable<Logradouro> ProjetarLogradourosAsync(
        IGeoFonteDados fonte,
        ResolucaoLocalidades resolucao,
        ContadorTabela contador,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (LogradouroCru cru in fonte.LerLogradourosAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();

            // FK cidade obrigatória: órfão (cidade não carregada) é descartado e contado.
            if (cru.CidadeIdDne is not int idCidade
                || !resolucao.CidadesPorIdDne.TryGetValue(idCidade, out Guid cidadeGuid))
            {
                contador.ContarOrfao(cru.Cep ?? "(sem cep)");
                continue;
            }

            // FK distrito/bairro opcionais: id ausente/não resolvido vira null (a fonte
            // traz distrito nulo com frequência), sem descartar a linha. Coerência
            // hierárquica (o domínio delega ao ETL, ver Logradouro): se a localidade
            // resolvida pertence a outra cidade, degrada para null e conta — não vincula
            // logradouro da cidade A a um distrito/bairro da cidade B.
            Guid? distritoGuid = ResolverCoerente(resolucao.DistritosPorIdDne, cru.DistritoIdDne, cidadeGuid, contador);
            Guid? bairroGuid = ResolverCoerente(resolucao.BairrosPorIdDne, cru.BairroIdDne, cidadeGuid, contador);

            // Parse único: deriva lat/lon decimais do Point já validado (PontoFactory
            // limita a ±90/±180), evitando parsear o texto duas vezes (×~1,4M) e duplicar
            // a regra do domínio geográfico — e a derivação mantém lat/lon dentro do
            // numeric(9,6) por construção.
            Point? coordenada = PontoFactory.Criar(cru.Latitude, cru.Longitude);
            decimal? latitude = coordenada is null ? null : (decimal)coordenada.Y;
            decimal? longitude = coordenada is null ? null : (decimal)coordenada.X;

            Result<Logradouro> resultado = Logradouro.Importar(
                cru.Cep ?? string.Empty,
                cru.Tipo,
                cru.Nome ?? string.Empty,
                cru.NomeCompleto,
                cru.NomeSemAcento ?? cru.Nome ?? string.Empty,
                cidadeGuid,
                distritoGuid,
                bairroGuid,
                cru.Uf ?? string.Empty,
                latitude,
                longitude,
                coordenada,
                ParseTolerante.ParaBoolSn(cru.CepAtivo),
                fonte.Versao);

            if (resultado.IsFailure)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            yield return resultado.Value!;
        }
    }

    private static async Task EscreverComplementoAsync(NpgsqlBinaryImporter importer, LogradouroComplemento e, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await importer.WriteAsync(e.Id, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.Cep, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.Complemento, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.ComplementoNormalizado, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.VersaoDataset, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(agora, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EscreverLogradouroAsync(NpgsqlBinaryImporter importer, Logradouro e, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await importer.WriteAsync(e.Id, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.Cep, cancellationToken).ConfigureAwait(false);
        await EscreverTextoAsync(importer, e.Tipo, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.Nome, cancellationToken).ConfigureAwait(false);
        await EscreverTextoAsync(importer, e.NomeCompleto, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.NomeNormalizado, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.CidadeId, cancellationToken).ConfigureAwait(false);
        await EscreverGuidAsync(importer, e.DistritoId, cancellationToken).ConfigureAwait(false);
        await EscreverGuidAsync(importer, e.BairroId, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.Uf, cancellationToken).ConfigureAwait(false);
        await EscreverDecimalAsync(importer, e.Latitude, cancellationToken).ConfigureAwait(false);
        await EscreverDecimalAsync(importer, e.Longitude, cancellationToken).ConfigureAwait(false);
        await EscreverPontoAsync(importer, e.Coordenada, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.Ativo, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(e.VersaoDataset, cancellationToken).ConfigureAwait(false);
        await importer.WriteAsync(agora, cancellationToken).ConfigureAwait(false);
    }

    // Loop de COPY em lotes: cada lote é um BeginBinaryImport...Complete próprio (commit
    // autocommit ao Complete); o staging acumula os lotes. Retorna o total escrito.
    private async Task<long> CopiarEmLotesAsync<T>(
        NpgsqlConnection conexao,
        string copyComando,
        IAsyncEnumerable<T> itens,
        Func<NpgsqlBinaryImporter, T, DateTimeOffset, CancellationToken, Task> escrever,
        DateTimeOffset agora,
        string tabela,
        CancellationToken cancellationToken)
    {
        long total = 0;
        int noLote = 0;
        NpgsqlBinaryImporter? importer = null;
        try
        {
            await foreach (T item in itens.ConfigureAwait(false))
            {
                importer ??= await conexao.BeginBinaryImportAsync(copyComando, cancellationToken).ConfigureAwait(false);
                await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
                await escrever(importer, item, agora, cancellationToken).ConfigureAwait(false);
                total++;
                noLote++;

                if (noLote >= TamanhoLote)
                {
                    await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
                    await importer.DisposeAsync().ConfigureAwait(false);
                    importer = null;
                    noLote = 0;
                    LogLoteConcluido(_logger, tabela, total);
                }
            }

            if (importer is not null)
            {
                await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (importer is not null)
            {
                await importer.DisposeAsync().ConfigureAwait(false);
            }
        }

        return total;
    }

    // Merge staging → alvo. O command tag do INSERT ... ON CONFLICT DO UPDATE conta as
    // chaves distintas afetadas (inseridas + atualizadas); o crescimento real do alvo dá
    // os inseridos; o resto, os atualizados; e staged − afetadas, as duplicatas removidas
    // pelo DISTINCT ON. O split assume execução exclusiva do ETL (single-writer) — não há
    // DML concorrente entre as contagens; é métrica de relatório, não controle de dados.
    private static async Task MergearContandoAsync(
        NpgsqlConnection conexao,
        ContadorTabela contador,
        string tabelaAlvo,
        string mergeSql,
        long staged,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        long antes = await ContarAsync(conexao, tabelaAlvo, cancellationToken).ConfigureAwait(false);

        long afetadas;
        NpgsqlCommand comando = conexao.CreateCommand();
        await using (comando.ConfigureAwait(false))
        {
            comando.CommandText = mergeSql;
            comando.Parameters.AddWithValue("now", agora);
            afetadas = await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        long depois = await ContarAsync(conexao, tabelaAlvo, cancellationToken).ConfigureAwait(false);

        long inseridos = depois - antes;
        long atualizados = afetadas - inseridos;
        long duplicados = staged - afetadas;

        contador.ContarInseridos((int)Math.Max(0, inseridos));
        contador.ContarAtualizados((int)Math.Max(0, atualizados));
        contador.ContarDuplicados((int)Math.Max(0, duplicados));
    }

    private static async Task<long> ContarAsync(NpgsqlConnection conexao, string tabela, CancellationToken cancellationToken)
    {
        NpgsqlCommand comando = conexao.CreateCommand();
        await using (comando.ConfigureAwait(false))
        {
            // Nome de tabela é literal de código (lista fixa), não entrada de usuário.
            comando.CommandText = $"SELECT count(*) FROM {tabela}";
            object? resultado = await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return resultado is long valor ? valor : 0;
        }
    }

    private static async Task DroparIndicesPesadosAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        await ExecutarAsync(conexao, "DROP INDEX IF EXISTS ix_logradouro_nome_trgm;", cancellationToken).ConfigureAwait(false);
        await ExecutarAsync(conexao, "DROP INDEX IF EXISTS ix_logradouro_coordenada;", cancellationToken).ConfigureAwait(false);
    }

    // CREATE INDEX CONCURRENTLY não pode rodar em transação: esta conexão está em
    // autocommit (nunca abrimos BeginTransaction nela). DROP IF EXISTS antes de cada
    // CREATE limpa eventual índice INVALID deixado por uma recriação anterior abortada.
    private static async Task RecriarIndicesPesadosAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        await ExecutarAsync(conexao, "DROP INDEX IF EXISTS ix_logradouro_nome_trgm;", cancellationToken).ConfigureAwait(false);
        await ExecutarAsync(conexao, "CREATE INDEX CONCURRENTLY ix_logradouro_nome_trgm ON logradouro USING gin (nome_normalizado gin_trgm_ops);", cancellationToken).ConfigureAwait(false);
        await ExecutarAsync(conexao, "DROP INDEX IF EXISTS ix_logradouro_coordenada;", cancellationToken).ConfigureAwait(false);
        await ExecutarAsync(conexao, "CREATE INDEX CONCURRENTLY ix_logradouro_coordenada ON logradouro USING gist (coordenada);", cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql, CancellationToken cancellationToken)
    {
        NpgsqlCommand comando = conexao.CreateCommand();
        await using (comando.ConfigureAwait(false))
        {
            comando.CommandText = sql;
            await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Resolve a FK opcional (distrito/bairro) garantindo coerência com a cidade do
    // logradouro: id ausente/não resolvido → null silencioso (FK opcional); resolvido mas
    // de outra cidade → null + contagem de degradação (inconsistência da fonte).
    private static Guid? ResolverCoerente(
        Dictionary<int, LocalidadeResolvida> mapa,
        int? idDne,
        Guid cidadeGuid,
        ContadorTabela contador)
    {
        if (idDne is not int id || !mapa.TryGetValue(id, out LocalidadeResolvida resolvida))
        {
            return null;
        }

        if (resolvida.CidadeId != cidadeGuid)
        {
            contador.ContarParseDegradado($"fk_localidade_cidade_divergente:{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            return null;
        }

        return resolvida.Id;
    }

    private static async Task EscreverTextoAsync(NpgsqlBinaryImporter importer, string? valor, CancellationToken cancellationToken)
    {
        if (valor is null)
        {
            await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await importer.WriteAsync(valor, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EscreverGuidAsync(NpgsqlBinaryImporter importer, Guid? valor, CancellationToken cancellationToken)
    {
        if (valor is null)
        {
            await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await importer.WriteAsync(valor.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EscreverDecimalAsync(NpgsqlBinaryImporter importer, decimal? valor, CancellationToken cancellationToken)
    {
        if (valor is null)
        {
            await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await importer.WriteAsync(valor.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EscreverPontoAsync(NpgsqlBinaryImporter importer, Point? valor, CancellationToken cancellationToken)
    {
        if (valor is null)
        {
            await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await importer.WriteAsync(valor, cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: COPY {Tabela} — {LinhasAcumuladas} linhas enviadas ao staging.")]
    private static partial void LogLoteConcluido(ILogger logger, string tabela, long linhasAcumuladas);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao recriar índices pesados de logradouro após carga inicial abortada — reexecute o modo Inicial para restaurá-los.")]
    private static partial void LogRecriacaoIndicesFalhou(ILogger logger, Exception excecao);
}
