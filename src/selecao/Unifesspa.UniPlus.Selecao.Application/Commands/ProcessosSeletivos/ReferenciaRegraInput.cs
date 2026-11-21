namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A regra do catálogo que o pedido referencia, pela identidade: código e versão. A leitura devolve a mesma referência com
/// o resumo da regra, e a escrita lê dela só o código e a versão.
/// </summary>
/// <param name="Codigo">Código da regra no catálogo de regras.</param>
/// <param name="Versao">Versão da regra no catálogo de regras.</param>
public sealed record ReferenciaRegraInput(string? Codigo, string? Versao);
