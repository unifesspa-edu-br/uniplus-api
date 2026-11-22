namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Forma de aceite de um termo de consentimento/declaração (termos imunes após a publicação, UNI-REQ-0086).
/// O código de contrato de cada membro nunca muda. A ordem de declaração é a de exibição.
/// </summary>
public enum FormaAceite
{
    /// <summary>Valor provisório — a publicação de um processo recusa termo exigido nesta forma.</summary>
    [CodigoDeContrato("A_DEFINIR", "A definir")]
    ADefinir = 1,

    /// <summary>Registro digital do aceite, sem log de endereço IP.</summary>
    [CodigoDeContrato("REGISTRO_DIGITAL_SEM_LOG_IP", "Registro digital sem log do endereço IP")]
    RegistroDigitalSemLogIp = 2,

    /// <summary>Registro digital do aceite, com log de endereço IP.</summary>
    [CodigoDeContrato("REGISTRO_DIGITAL_COM_LOG_IP", "Registro digital com log do endereço IP")]
    RegistroDigitalComLogIp = 3,
}
