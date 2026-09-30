namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma opção que o processo oferece num campo de fato cuja fonte dos valores é o processo
/// (ADR-0136): o código que a resposta e os predicados citam, o rótulo exibido e a ordem.
/// </summary>
public sealed record OpcaoDoProcesso(string Codigo, string Rotulo, int Ordem);
