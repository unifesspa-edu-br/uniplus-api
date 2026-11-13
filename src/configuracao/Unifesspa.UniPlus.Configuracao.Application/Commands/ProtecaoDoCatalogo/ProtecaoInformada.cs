namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

/// <summary>
/// A proteção que o Representante do controlador conferiu, como o corpo da requisição a informa, ainda não conferida:
/// <see cref="PedidoDeDecisao"/> a lê como <c>ProtecaoDeDados</c>, com as demais recusas do lote (ADR-0125).
/// </summary>
/// <param name="ClassificacaoProtecao">Token canônico da classificação, como a listagem a mostra.</param>
/// <param name="HipoteseLegal">Token canônico da hipótese legal.</param>
/// <param name="FinalidadeTratamento">A finalidade do tratamento.</param>
public sealed record ProtecaoInformada(string? ClassificacaoProtecao, string? HipoteseLegal, string? FinalidadeTratamento);
