namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.IO;
using System.Reflection;

/// <summary>
/// Descreve um schema Avro a registrar no Schema Registry no startup da API.
/// </summary>
/// <remarks>
/// <para>
/// Cada módulo (Selecao, Ingresso, ...) registra suas instâncias via
/// <c>AddSchemaRegistry().AddSchema(...)</c>. O hosted service
/// <c>SchemaRegistrationHostedService</c> lê todas as instâncias registradas no DI e
/// faz <c>RegisterSchemaAsync</c> idempotente para cada uma — Apicurio retorna o mesmo
/// ID se o schema já existe (BACKWARD compatibility check enforced server-side quando
/// configurado no chart).
/// </para>
/// <para>
/// O <see cref="Subject"/> segue a convenção Confluent SR <c>&lt;topic&gt;-value</c>
/// (e <c>&lt;topic&gt;-key</c> quando o producer também serializar a key).
/// </para>
/// </remarks>
/// <param name="Subject">Subject Confluent SR (e.g. <c>edital_events-value</c>).</param>
/// <param name="SchemaResourceName">
/// Nome canônico do embedded resource (e.g.
/// <c>Unifesspa.UniPlus.Selecao.Domain.Events.Schemas.EditalPublicado.avsc</c>).
/// </param>
/// <param name="ResourceAssembly">
/// Assembly que contém o embedded resource — tipicamente o <c>.Domain</c> do módulo.
/// </param>
public sealed record SchemaRegistration(
    string Subject,
    string SchemaResourceName,
    Assembly ResourceAssembly)
{
    /// <summary>
    /// Lê o conteúdo do schema do embedded resource. Pode lançar
    /// <see cref="InvalidOperationException"/> se o resource não for encontrado —
    /// indica problema de empacotamento do assembly de Domain (csproj sem
    /// <c>&lt;EmbeddedResource&gt;</c> ou nome de resource desalinhado).
    /// </summary>
    public string ReadSchemaContent()
    {
        using Stream? stream = ResourceAssembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Schema Avro '{SchemaResourceName}' não encontrado como embedded resource em '{ResourceAssembly.FullName}'.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
