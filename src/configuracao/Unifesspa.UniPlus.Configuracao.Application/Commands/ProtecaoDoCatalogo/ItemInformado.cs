namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

/// <summary>
/// O item como o corpo da requisição o informa, ainda não conferido: <see cref="PedidoDeDecisao"/> o lê como
/// <c>ItemDaProtecao</c>, com as demais recusas do lote (ADR-0125).
/// </summary>
/// <param name="Cadastro"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>.</param>
/// <param name="ItemId">Identificador do tipo de documento ou do fato.</param>
public sealed record ItemInformado(string? Cadastro, Guid ItemId);
