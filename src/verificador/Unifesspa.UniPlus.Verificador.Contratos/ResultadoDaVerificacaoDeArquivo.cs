namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O veredicto que o verificador devolve pela fila de resultados. Nunca traz o conteúdo nem o
/// nome da ameaça, que fica só no log do verificador.
/// </summary>
/// <param name="ArquivoId">O arquivo do pedido.</param>
/// <param name="Veredicto">O desfecho da verificação.</param>
/// <param name="Motivo">Por que foi recusado; <see cref="MotivoDaRecusa.NaoSeAplica"/> fora da recusa.</param>
/// <param name="HashSha256">O SHA-256 do conteúdo verificado; só no verificado.</param>
/// <param name="TamanhoBytes">O tamanho do conteúdo verificado; só no verificado.</param>
/// <param name="Formato">O formato conferido no conteúdo; só no verificado.</param>
public sealed record ResultadoDaVerificacaoDeArquivo(
    Guid ArquivoId,
    VeredictoDaVerificacao Veredicto,
    MotivoDaRecusa Motivo,
    string? HashSha256,
    long? TamanhoBytes,
    FormatoDoArquivo? Formato);
