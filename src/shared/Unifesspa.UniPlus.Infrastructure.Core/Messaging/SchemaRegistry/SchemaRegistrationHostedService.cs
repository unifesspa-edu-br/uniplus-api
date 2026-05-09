namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service idempotente que registra os schemas Avro conhecidos do módulo no
/// Schema Registry (Apicurio) durante o startup do host.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-graceful seletivo:</b> apenas falhas <i>transientes</i> (Apicurio offline,
/// DNS lookup falhou, timeout do HttpClient — capturadas como
/// <see cref="HttpRequestException"/>, <see cref="SocketException"/> ou
/// <see cref="TaskCanceledException"/>) são silenciadas — o host segue subindo e o
/// producer Wolverine retenta em runtime via Confluent serdes. Falhas
/// <b>determinísticas</b> (<c>SchemaRegistryException</c> = auth 401/403,
/// conflict 409, malformed 422; <see cref="InvalidOperationException"/> de embedded
/// resource ausente; <c>AvroException</c> de schema inválido) <b>propagam e travam
/// StartAsync</b> — release ruim não pode rodar em modo "degraded but accepting
/// traffic". Sem o filtro, schemas com bug real entram em loop runtime de "fail to
/// publish" e mensagens durables se acumulam silenciosamente no outbox.
/// </para>
/// <para>
/// <b>Idempotência:</b> Apicurio aceita registrar o mesmo schema (byte-equal) múltiplas
/// vezes — retorna o mesmo schema-id. Mudança de schema dispara o compatibility check
/// configurado (default BACKWARD); incompatibilidade falha o registro com 409 Conflict
/// e o serviço propaga via log error (não trava boot).
/// </para>
/// <para>
/// <b>Filtro de assemblies:</b> escaneia <see cref="SchemaRegistration"/>s registradas
/// no DI por <c>AddSchemaRegistry().AddSchema(...)</c>. Múltiplos módulos podem registrar
/// schemas no mesmo host sem conflito — cada módulo declara apenas seus subjects.
/// </para>
/// <para>
/// Posicionado <b>após</b> o <c>StartHostedService</c> do Wolverine no pipeline para que
/// o serviço de mensageria já esteja inicializado quando publicarmos. Ordem importa só
/// para o caminho feliz (boot subsequente após Apicurio online); fail-graceful resolve
/// o resto.
/// </para>
/// </remarks>
public sealed partial class SchemaRegistrationHostedService : IHostedService
{
    private readonly ISchemaRegistryClient schemaRegistryClient;
    private readonly IReadOnlyCollection<SchemaRegistration> registrations;
    private readonly ILogger<SchemaRegistrationHostedService> logger;

    public SchemaRegistrationHostedService(
        ISchemaRegistryClient schemaRegistryClient,
        IEnumerable<SchemaRegistration> registrations,
        ILogger<SchemaRegistrationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(schemaRegistryClient);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(logger);

        this.schemaRegistryClient = schemaRegistryClient;
        this.registrations = [.. registrations];
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (registrations.Count == 0)
        {
            LogNoSchemasRegistered(logger);
            return;
        }

        LogRegisteringSchemas(logger, registrations.Count);

        foreach (SchemaRegistration registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string schemaContent = registration.ReadSchemaContent();
                Schema schema = new(schemaContent, SchemaType.Avro);

                int schemaId = await schemaRegistryClient
                    .RegisterSchemaAsync(registration.Subject, schema, normalize: true)
                    .ConfigureAwait(false);

                LogSchemaRegistered(logger, registration.Subject, schemaId);
            }
            // Fail-graceful **apenas** para falhas transientes — Apicurio offline,
            // DNS lookup falhou, timeout do HttpClient. Confluent serdes faz retry
            // em runtime quando o Registry voltar.
            catch (HttpRequestException ex)
            {
                LogSchemaRegistrationTransientFailure(logger, registration.Subject, ex);
            }
            catch (SocketException ex)
            {
                LogSchemaRegistrationTransientFailure(logger, registration.Subject, ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken == default)
            {
                LogSchemaRegistrationTransientFailure(logger, registration.Subject, ex);
            }
            // Falhas determinísticas (SchemaRegistryException = auth/conflict/malformed,
            // InvalidOperationException = embedded resource ausente, AvroException =
            // schema sintaticamente inválido) propagam e travam StartAsync — release
            // ruim não pode rodar em modo "degraded but accepting traffic".
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Nenhum schema Avro a registrar no Schema Registry — feature off ou módulo sem eventos cross-module.")]
    private static partial void LogNoSchemasRegistered(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Registrando {Count} schema(s) Avro no Schema Registry no startup.")]
    private static partial void LogRegisteringSchemas(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Schema Avro registrado/confirmado. Subject={Subject} SchemaId={SchemaId}")]
    private static partial void LogSchemaRegistered(ILogger logger, string subject, int schemaId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha transiente ao registrar schema Avro no startup (continuando com host degradado — registro tentará novamente em runtime via Confluent serdes). Subject={Subject}")]
    private static partial void LogSchemaRegistrationTransientFailure(ILogger logger, string subject, Exception exception);
}
