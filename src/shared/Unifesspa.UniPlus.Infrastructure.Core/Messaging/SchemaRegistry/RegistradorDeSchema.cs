namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System.Net.Sockets;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Logging;

/// <summary>
/// Registra um schema no Schema Registry, de forma idempotente, depois de conferir a regra de compatibilidade que o
/// formato exige do subject. É a única via de registro: a subida e a nova tentativa passam por ela.
/// </summary>
/// <remarks>
/// Só a indisponibilidade do Schema Registry (conexão recusada, DNS, tempo esgotado) vira
/// <see cref="ResultadoDoRegistroDeSchema.SchemaRegistryIndisponivel"/>. Qualquer outra falha é determinística (credencial,
/// conflito de compatibilidade, schema malformado, regra diferente da exigida, recurso ausente) e propaga: release com
/// configuração ruim não pode rodar em modo degradado. O cliente Confluent reembrulha a falha de conexão numa
/// <see cref="HttpRequestException"/> sem o código do erro, então a causa é procurada na cadeia de exceções.
/// </remarks>
internal sealed partial class RegistradorDeSchema(
    ISchemaRegistryClient schemaRegistry,
    ILeitorDaRegraDeCompatibilidadeDoSubject leitorDaRegra,
    ILogger<RegistradorDeSchema> logger)
{
    public async Task<ResultadoDoRegistroDeSchema> RegistrarAsync(SchemaRegistration registro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registro);

        try
        {
            await RegistrarNoSchemaRegistryAsync(registro, cancellationToken).ConfigureAwait(false);

            return ResultadoDoRegistroDeSchema.Confirmado;
        }
        catch (Exception falha) when (EhIndisponibilidadeDoRegistry(falha))
        {
            LogSchemaRegistryIndisponivel(logger, registro.Subject, falha);

            return ResultadoDoRegistroDeSchema.SchemaRegistryIndisponivel;
        }
    }

    private async Task RegistrarNoSchemaRegistryAsync(SchemaRegistration registro, CancellationToken cancellationToken)
    {
        Schema schema = new(registro.ReadSchemaContent(), registro.Formato.Tipo);
        await ConferirRegraAsync(registro, cancellationToken).ConfigureAwait(false);
        int identificador = await schemaRegistry.RegisterSchemaAsync(registro.Subject, schema, normalize: true).ConfigureAwait(false);

        LogSchemaRegistrado(logger, registro.Subject, identificador);
    }

    private async Task ConferirRegraAsync(SchemaRegistration registro, CancellationToken cancellationToken)
    {
        if (registro.Formato.RegraDeCompatibilidadeExigida is not { } exigida)
        {
            return;
        }

        Compatibility encontrada = await leitorDaRegra.LerRegraDeCompatibilidadeEfetivaAsync(registro.Subject, cancellationToken).ConfigureAwait(false);
        if (encontrada != exigida)
        {
            throw new InvalidOperationException(
                $"O subject '{registro.Subject}' tem a regra de compatibilidade {encontrada}, e o {registro.Formato.Nome} exige {exigida}. "
                + "A regra é declarada pela infraestrutura do Schema Registry, e não pela API.");
        }
    }

    private static bool EhIndisponibilidadeDoRegistry(Exception falha) => falha switch
    {
        HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError } => true,
        SocketException => true,
        TaskCanceledException cancelada => cancelada.InnerException is TimeoutException || cancelada.CancellationToken == default,
        _ => falha.InnerException is { } causa && EhIndisponibilidadeDoRegistry(causa),
    };

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Schema registrado/confirmado no Schema Registry. Subject={Subject} SchemaId={SchemaId}")]
    private static partial void LogSchemaRegistrado(ILogger logger, string subject, int schemaId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Schema Registry indisponível ao registrar o schema; o host segue e o registro será refeito em runtime. Subject={Subject}")]
    private static partial void LogSchemaRegistryIndisponivel(ILogger logger, string subject, Exception exception);
}
