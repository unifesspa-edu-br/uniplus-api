namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Forma de aceite de um termo de consentimento/declaração (termos imunes após a publicação, UNI-REQ-0086).
/// O código de contrato de cada membro nunca muda. A ordem de declaração é a de exibição.
/// </summary>
[DescricaoDoContrato("Como o candidato registra o aceite de um termo de consentimento ou declaração.")]
public enum FormaAceite
{
    /// <summary>Valor provisório — a publicação de um processo recusa termo exigido nesta forma.</summary>
    [CodigoDeContrato("A_DEFINIR", "A definir", Descricao = "Forma ainda não definida; a publicação de um processo recusa termo exigido nesta forma.")]
    ADefinir = 1,

    /// <summary>Registro digital do aceite, sem guardar o endereço IP.</summary>
    [CodigoDeContrato("REGISTRO_SEM_ENDERECO_IP", "Registro digital sem o endereço IP", Descricao = "O aceite é registrado no sistema sem guardar o endereço IP do candidato.")]
    RegistroSemEnderecoIp = 2,

    /// <summary>Registro digital do aceite, guardando o endereço IP.</summary>
    [CodigoDeContrato("REGISTRO_COM_ENDERECO_IP", "Registro digital com o endereço IP", Descricao = "O aceite é registrado no sistema guardando o endereço IP do candidato.")]
    RegistroComEnderecoIp = 3,
}
