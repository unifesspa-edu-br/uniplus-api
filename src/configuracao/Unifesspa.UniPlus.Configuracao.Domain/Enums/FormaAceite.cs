namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Forma de aceite de um termo de consentimento/declaração (UNI-REQ-0086/RN-COL-05).
/// Persistida como token UPPER_SNAKE (<see cref="FormasAceite"/>).
/// </summary>
public enum FormaAceite
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Placeholder — a publicação de um processo recusa termo exigido nesta forma.</summary>
    ADefinir = 1,

    /// <summary>Registro digital do aceite, sem log de endereço IP.</summary>
    RegistroDigitalSemLogIp = 2,

    /// <summary>Registro digital do aceite, com log de endereço IP.</summary>
    RegistroDigitalComLogIp = 3,
}
