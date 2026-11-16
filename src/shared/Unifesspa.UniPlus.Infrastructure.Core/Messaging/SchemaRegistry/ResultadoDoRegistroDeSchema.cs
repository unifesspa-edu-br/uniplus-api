namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>O que o registro de um schema deixou: confirmado, ou adiado porque o Schema Registry está indisponível.</summary>
internal enum ResultadoDoRegistroDeSchema
{
    Confirmado,
    SchemaRegistryIndisponivel,
}
