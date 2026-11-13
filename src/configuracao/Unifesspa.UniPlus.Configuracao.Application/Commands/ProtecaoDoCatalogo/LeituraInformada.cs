namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

/// <summary>
/// O que o Representante do controlador leu do circuito do item, como o corpo da requisição o informa, ainda não
/// conferido: <see cref="PedidoDeDecisao"/> o lê como <c>LeituraDoCircuito</c>, com as demais recusas do lote (ADR-0125).
/// </summary>
/// <param name="Ciclo">O <c>aprovacao.ciclo</c> lido no item.</param>
/// <param name="ManifestacoesNoCiclo">
/// O <c>aprovacao.manifestacoesNoCiclo</c> lido no item: a manifestação registrada depois da leitura faz a decisão
/// responder conflito (UNI-REQ-0374).
/// </param>
public sealed record LeituraInformada(int? Ciclo, int? ManifestacoesNoCiclo);
