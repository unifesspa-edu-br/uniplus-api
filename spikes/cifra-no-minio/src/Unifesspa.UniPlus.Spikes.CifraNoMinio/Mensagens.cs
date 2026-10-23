namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

/// <summary>
/// Pedido de verificação, publicado pela API na confirmação do envio, na mesma transação que grava
/// o arquivo como "em verificação". Leva a chave do objeto e o ETag da versão que o candidato
/// confirmou: nenhum byte do conteúdo.
/// </summary>
public sealed record VerificarArquivo(string Chave, string ETag, DateTimeOffset ConfirmadoEm);

/// <summary>
/// O veredicto que o verificador devolve à API. Nunca leva o conteúdo nem o nome da ameaça.
/// <c>Veredicto</c>: LIMPO; INFECTADO; FORA_DO_LIMITE (acima do tamanho máximo, recusado sem ser
/// lido); ou NAO_ENCONTRADO (a versão confirmada não está no bucket de envio nem no definitivo: não
/// foi enviada, foi substituída por outro PUT ou expirou na quarentena antes de ser verificada).
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
