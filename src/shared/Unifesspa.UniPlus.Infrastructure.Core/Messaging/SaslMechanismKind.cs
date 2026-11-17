namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>Situação do mecanismo SASL informado na configuração, quanto ao que o Uni+ aceita.</summary>
internal enum SaslMechanismKind
{
    /// <summary>Não informado.</summary>
    Ausente,

    /// <summary>Informado, mas que o cliente Kafka não reconhece.</summary>
    Desconhecido,

    /// <summary>Reconhecido, mas que a validação não sabe conferir (autentica sem usuário e senha).</summary>
    NaoSuportado,

    /// <summary>Reconhecido e autenticado por usuário e senha: PLAIN ou SCRAM.</summary>
    UsuarioESenha,
}
