namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.AbrirRetificacao"/> — só o motivo. A
/// versão que a sessão retifica <b>não</b> é informada pelo cliente: o servidor a infere do
/// topo da cadeia (ADR-0101), sob o mesmo lock que abre o rascunho.
/// </summary>
public sealed record AbrirRetificacaoRequest(string Motivo);
