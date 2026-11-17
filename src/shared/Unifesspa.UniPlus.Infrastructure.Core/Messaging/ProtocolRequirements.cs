namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Confluent.Kafka;

/// <summary>O que um protocolo de segurança do Kafka exige da configuração: autenticação SASL e transporte SSL.</summary>
internal readonly record struct ProtocolRequirements(bool Sasl, bool Ssl)
{
    public static ProtocolRequirements De(SecurityProtocol protocolo) => new(
        Sasl: protocolo is SecurityProtocol.SaslPlaintext or SecurityProtocol.SaslSsl,
        Ssl: protocolo is SecurityProtocol.Ssl or SecurityProtocol.SaslSsl);
}
