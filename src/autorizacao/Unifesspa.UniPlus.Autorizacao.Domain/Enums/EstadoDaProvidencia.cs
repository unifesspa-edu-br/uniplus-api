namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O estado gravado da providência, que só os atos dela mudam (UNI-REQ-0376). Não existe estado "prejudicada": ela é
/// derivada na leitura (<see cref="SituacaoDaProvidencia"/>), e o tipo gravado não tem como representá-la.
/// </summary>
public enum EstadoDaProvidencia
{
    /// <summary>Aguarda resposta ou atendimento.</summary>
    [CodigoDeContrato("ABERTA", "Aberta")]
    Aberta = 1,

    /// <summary>Respondida com motivo, sem revogar a concessão.</summary>
    [CodigoDeContrato("RESPONDIDA", "Respondida")]
    Respondida = 2,

    /// <summary>Atendida pela revogação vinculada da concessão.</summary>
    [CodigoDeContrato("ATENDIDA", "Atendida")]
    Atendida = 3,
}
