namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// A situação da providência como a leitura a mostra: o estado gravado e, além dele, a providência aberta cuja concessão
/// deixou de valer, por revogação feita por outro caminho ou por vencimento, que fica prejudicada sem que nada seja
/// gravado (UNI-REQ-0376).
/// </summary>
public enum SituacaoDaProvidencia
{
    [CodigoDeContrato("ABERTA", "Aberta")]
    Aberta = 1,
    [CodigoDeContrato("RESPONDIDA", "Respondida")]
    Respondida = 2,
    [CodigoDeContrato("ATENDIDA", "Atendida")]
    Atendida = 3,

    /// <summary>Aberta, mas a concessão já não está ativa no instante da leitura.</summary>
    [CodigoDeContrato("PREJUDICADA", "Prejudicada")]
    Prejudicada = 4,
}
