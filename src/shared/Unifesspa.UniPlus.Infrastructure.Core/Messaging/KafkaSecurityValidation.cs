namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using System.Diagnostics;

using Confluent.Kafka;

/// <summary>Nomes dos campos de segurança numa seção de configuração, para as mensagens de validação.</summary>
internal sealed record KafkaSecurityFields(
    string Section,
    string Protocolo,
    string Mecanismo,
    string Usuario,
    string Senha,
    string CaLocation,
    string? CaPem);

/// <summary>
/// Validação cruzada de segurança do cliente Kafka (protocolo, SASL, SSL), a mesma para todo uso que conecta ao broker:
/// o transporte do Wolverine e o registro de acesso. Cada um informa os nomes dos próprios campos.
/// </summary>
/// <remarks>
/// Cada regra devolve as falhas dela, e <see cref="Validar"/> as junta na ordem de declaração. Protocolo não
/// reconhecido encerra a validação: as demais regras dependem do que o protocolo exige.
/// </remarks>
internal static class KafkaSecurityValidation
{
    public static List<string> Validar(
        KafkaSecurityFields campos,
        string? securityProtocol,
        string? saslMechanism,
        string? usuario,
        string? senha,
        string? caLocation,
        string? caPem)
    {
        if (!TryParseProtocol(securityProtocol, out SecurityProtocol protocolo))
        {
            return [ProtocoloInvalido(campos, securityProtocol)];
        }
        ProtocolRequirements exigencias = ProtocolRequirements.De(protocolo);
        return
        [
            .. Falhas(
                CoerenciaDoSasl(campos, AlgumPreenchido(saslMechanism, usuario, senha), exigencias),
                CoerenciaDoSsl(campos, AlgumPreenchido(caLocation, caPem), exigencias),
                CaAmbiguo(campos, caLocation, caPem)),
            .. exigencias.Sasl ? FalhasDoSasl(campos, securityProtocol, saslMechanism, usuario, senha) : [],
            .. Falhas(CaObrigatorio(campos, securityProtocol, caLocation, caPem, exigencias)),
        ];
    }

    private static IEnumerable<string> Falhas(params string?[] falhas) => falhas.OfType<string>();

    private static bool AlgumPreenchido(params string?[] valores) =>
        valores.Any(valor => !string.IsNullOrWhiteSpace(valor));

    /// <summary>Protocolo ausente vale PLAINTEXT; informado e não reconhecido não vale nenhum.</summary>
    private static bool TryParseProtocol(string? texto, out SecurityProtocol protocolo)
    {
        protocolo = SecurityProtocol.Plaintext;
        return string.IsNullOrWhiteSpace(texto) || KafkaSecurity.TryParseByName(texto, out protocolo);
    }

    private static string ProtocoloInvalido(KafkaSecurityFields campos, string? securityProtocol) =>
        $"{campos.Section}:{campos.Protocolo} '{securityProtocol}' inválido. Use PLAINTEXT, SSL, SASL_PLAINTEXT ou SASL_SSL.";

    // Credenciais com SecurityProtocol=PLAINTEXT fariam o cliente cair em PLAINTEXT silenciosamente: risco de segurança.
    private static string? CoerenciaDoSasl(KafkaSecurityFields campos, bool temCampoSasl, ProtocolRequirements exigencias) =>
        temCampoSasl && !exigencias.Sasl
            ? $"{campos.Section}:{campos.Mecanismo}/{campos.Usuario}/{campos.Senha} exigem {campos.Protocolo}=SASL_PLAINTEXT ou SASL_SSL."
            : null;

    private static string? CoerenciaDoSsl(KafkaSecurityFields campos, bool temCampoSsl, ProtocolRequirements exigencias)
    {
        if (!temCampoSsl || exigencias.Ssl)
        {
            return null;
        }

        string ca = campos.CaPem is null ? campos.CaLocation : $"{campos.CaLocation}/{campos.CaPem}";
        return $"{campos.Section}:{ca} exigem {campos.Protocolo}=SSL ou SASL_SSL.";
    }

    // CaLocation e CaPem são alternativas: preencher os dois é ambíguo.
    private static string? CaAmbiguo(KafkaSecurityFields campos, string? caLocation, string? caPem) =>
        campos.CaPem is not null && !string.IsNullOrWhiteSpace(caLocation) && !string.IsNullOrWhiteSpace(caPem)
            ? $"{campos.Section}:{campos.CaLocation} e {campos.Section}:{campos.CaPem} são mutuamente exclusivos — preencha apenas um."
            : null;

    private static IEnumerable<string> FalhasDoSasl(
        KafkaSecurityFields campos,
        string? securityProtocol,
        string? saslMechanism,
        string? usuario,
        string? senha) =>
        ClassificarMecanismo(saslMechanism) switch
        {
            SaslMechanismKind.Ausente => [Obrigatorio(campos, campos.Mecanismo, securityProtocol)],
            SaslMechanismKind.Desconhecido => [$"{campos.Section}:{campos.Mecanismo} '{saslMechanism}' inválido. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512."],
            SaslMechanismKind.NaoSuportado => [$"{campos.Section}:{campos.Mecanismo} '{saslMechanism}' não é suportado pelo Uni+. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512."],
            SaslMechanismKind.UsuarioESenha => FalhasDeUsuarioESenha(campos, securityProtocol, usuario, senha),
            _ => throw new UnreachableException(),
        };

    /// <summary>
    /// PLAIN e SCRAM-* exigem usuário e senha. OAUTHBEARER e GSSAPI ficam fora do escopo desta camada: quem escolhe um
    /// deles precisa estender a validação, não cair no caminho de usuário e senha em silêncio.
    /// </summary>
    private static SaslMechanismKind ClassificarMecanismo(string? saslMechanism)
    {
        if (string.IsNullOrWhiteSpace(saslMechanism))
        {
            return SaslMechanismKind.Ausente;
        }

        if (!KafkaSecurity.TryParseByName(saslMechanism, out SaslMechanism mecanismo))
        {
            return SaslMechanismKind.Desconhecido;
        }

        return mecanismo is SaslMechanism.Plain or SaslMechanism.ScramSha256 or SaslMechanism.ScramSha512
            ? SaslMechanismKind.UsuarioESenha
            : SaslMechanismKind.NaoSuportado;
    }

    private static IEnumerable<string> FalhasDeUsuarioESenha(
        KafkaSecurityFields campos,
        string? securityProtocol,
        string? usuario,
        string? senha) =>
        Falhas(
            string.IsNullOrWhiteSpace(usuario) ? Obrigatorio(campos, campos.Usuario, securityProtocol) : null,
            string.IsNullOrWhiteSpace(senha) ? Obrigatorio(campos, campos.Senha, securityProtocol) : null);

    private static string Obrigatorio(KafkaSecurityFields campos, string campo, string? securityProtocol) =>
        $"{campos.Section}:{campo} é obrigatório com {campos.Protocolo}={securityProtocol}.";

    // Política Uni+ standalone (ADR-009 do uniplus-infra): o cluster usa CA self-signed, então o CA explícito é
    // mandatório quando o transporte é SSL ou SASL_SSL.
    private static string? CaObrigatorio(
        KafkaSecurityFields campos,
        string? securityProtocol,
        string? caLocation,
        string? caPem,
        ProtocolRequirements exigencias)
    {
        if (!exigencias.Ssl || AlgumPreenchido(caLocation, caPem))
        {
            return null;
        }

        string ca = campos.CaPem is null ? campos.CaLocation : $"{campos.CaLocation} ou {campos.Section}:{campos.CaPem}";
        return $"{campos.Section}:{ca} é obrigatório com {campos.Protocolo}={securityProtocol} (política Uni+: CA explícito).";
    }
}
