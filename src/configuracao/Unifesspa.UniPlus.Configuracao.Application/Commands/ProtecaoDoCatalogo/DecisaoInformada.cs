namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

/// <summary>
/// A decisão com os seus fundamentos, como o corpo da requisição a informa, ainda não conferida:
/// <see cref="PedidoDeDecisao"/> a lê como <c>DecisaoFundamentada</c>, com as demais recusas do lote (ADR-0125).
/// </summary>
/// <param name="Decisao"><c>APROVAR</c>, <c>RECOMENDAR_AJUSTE</c> ou <c>REPROVAR</c>.</param>
/// <param name="ClassificacaoRecomendada">Token canônico da classificação; obrigatório no ajuste e recusado nas demais decisões.</param>
/// <param name="Justificativa">
/// Motivo da decisão, de até 2.000 caracteres: obrigatório na reprovação; na aprovação e no ajuste, opcional, salvo
/// diante de manifestação técnica desfavorável vigente, que o exige (UNI-REQ-0374). Fica registrado no ato da decisão.
/// </param>
public sealed record DecisaoInformada(string? Decisao, string? ClassificacaoRecomendada = null, string? Justificativa = null);
