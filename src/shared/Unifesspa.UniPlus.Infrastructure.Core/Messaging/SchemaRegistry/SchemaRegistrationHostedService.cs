namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service idempotente que registra os schemas (Avro e JSON Schema) conhecidos do módulo no
/// Schema Registry (Apicurio) durante o startup do host, pelo <see cref="RegistradorDeSchema"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-graceful seletivo:</b> apenas falhas transientes inspecionadas via
/// <see cref="HttpRequestException.HttpRequestError"/> (<c>ConnectionError</c> =
/// Apicurio offline / connection refused; <c>NameResolutionError</c> = DNS
/// resolução falhou) + <see cref="SocketException"/> + timeout
/// (<see cref="TaskCanceledException"/> com <see cref="TimeoutException"/> inner)
/// são silenciadas — host segue subindo, Confluent serdes retenta em runtime
/// quando o Registry voltar.
/// </para>
/// <para>
/// <b>Falhas determinísticas propagam e travam <c>StartAsync</c>:</b>
/// </para>
/// <list type="bullet">
/// <item><description><c>SchemaRegistryException</c> — auth 401/403, conflict 409, malformed 422</description></item>
/// <item><description><see cref="HttpRequestException"/> com <c>SecureConnectionError</c> (TLS chain inválida), <c>HttpProtocolError</c>, <c>UserAuthenticationError</c> (cliente cert), <c>InvalidResponse</c>, <c>ProxyTunnelError</c></description></item>
/// <item><description><see cref="InvalidOperationException"/> — embedded resource ausente, OAuth 4xx (config inválida)</description></item>
/// <item><description><c>AvroException</c> — schema sintaticamente inválido</description></item>
/// </list>
/// <para>
/// Release com config ruim não pode rodar em modo "degraded but accepting traffic" —
/// sem o filtro, schemas/auth com bug real entram em loop runtime de "fail to
/// publish" e mensagens durables se acumulam silenciosamente no outbox.
/// </para>
/// <para>
/// <b>JSON Schema:</b> sem serializador que registre de novo em runtime, o registro que falhou por indisponibilidade é
/// entregue à <see cref="RepetidorDoRegistroDeSchema"/>, e a subida segue; o subject do JSON Schema exige a regra <c>NONE</c>.
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
    private readonly RegistradorDeSchema _registrador;
    private readonly RepetidorDoRegistroDeSchema _novaTentativa;
    private readonly IReadOnlyCollection<SchemaRegistration> _registrations;
    private readonly ILogger<SchemaRegistrationHostedService> _logger;

    internal SchemaRegistrationHostedService(
        RegistradorDeSchema registrador,
        RepetidorDoRegistroDeSchema novaTentativa,
        IEnumerable<SchemaRegistration> registrations,
        ILogger<SchemaRegistrationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(registrador);
        ArgumentNullException.ThrowIfNull(novaTentativa);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(logger);

        _registrador = registrador;
        _novaTentativa = novaTentativa;
        _registrations = [.. registrations];
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_registrations.Count == 0)
        {
            LogNoSchemasRegistered(_logger);
            return;
        }

        LogRegisteringSchemas(_logger, _registrations.Count);

        foreach (SchemaRegistration registration in _registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RegistrarAsync(registration, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RegistrarAsync(SchemaRegistration registration, CancellationToken cancellationToken)
    {
        ResultadoDoRegistroDeSchema resultado = await _registrador.RegistrarAsync(registration, cancellationToken).ConfigureAwait(false);

        if (resultado == ResultadoDoRegistroDeSchema.SchemaRegistryIndisponivel && registration.Formato.RepeteRegistroAposFalhaTransitoria)
        {
            _novaTentativa.Agendar(registration);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Nenhum schema a registrar no Schema Registry — feature off ou módulo sem eventos cross-module.")]
    private static partial void LogNoSchemasRegistered(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Registrando {Count} schema(s) no Schema Registry no startup.")]
    private static partial void LogRegisteringSchemas(ILogger logger, int count);
}
