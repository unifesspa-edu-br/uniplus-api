namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

/// <summary>
/// Pedido de verificação, publicado pela API na confirmação do envio, na mesma transação que grava
/// o arquivo como "em verificação". Leva só a chave do objeto: nenhum byte do conteúdo.
/// </summary>
public sealed record VerificarArquivo(string Chave, DateTimeOffset ConfirmadoEm);

/// <summary>
/// O veredicto que o verificador devolve à API. Nunca leva o conteúdo nem o nome da ameaça.
/// <c>Veredicto</c>: LIMPO, INFECTADO ou NAO_ENCONTRADO (o objeto sumiu do bucket de envio sem
/// passar pelo verificador, por exemplo expirado).
/// </summary>
public sealed record ArquivoVerificado(
    string Chave,
    string Veredicto,
    string Sha256,
    long Bytes,
    DateTimeOffset InicioEm,
    DateTimeOffset ConcluidoEm,
    double VerificacaoMs,
    string Verificador);

/// <summary>O clamd não respondeu: falha passageira, que volta à fila e nunca recusa o arquivo.</summary>
public sealed class ClamdIndisponivelException(string chave)
    : Exception($"clamd indisponível ao verificar {chave}");
