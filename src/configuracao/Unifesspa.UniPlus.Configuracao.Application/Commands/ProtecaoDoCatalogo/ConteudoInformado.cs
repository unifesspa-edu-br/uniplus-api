namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

/// <summary>
/// O parecer e o texto da manifestação técnica, como o corpo da requisição os informa, ainda não conferidos:
/// <see cref="PedidoDeManifestacao"/> os lê como <c>ConteudoDaManifestacao</c>, com as demais recusas do lote (ADR-0125).
/// </summary>
/// <param name="Parecer"><c>ADEQUADA</c>, <c>RECOMENDA_AJUSTE</c> ou <c>INADEQUADA</c>.</param>
/// <param name="Texto">Até 2.000 caracteres; obrigatório em <c>RECOMENDA_AJUSTE</c> e <c>INADEQUADA</c>.</param>
public sealed record ConteudoInformado(string? Parecer, string? Texto = null);
