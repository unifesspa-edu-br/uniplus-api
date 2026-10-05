namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// A cópia para o acervo público não pôde ser feita.
/// </summary>
/// <remarks>
/// Distingue-se da falha transiente do banco por não se resolver necessariamente em segundos: o
/// armazenamento reiniciando, ou o bucket público ainda não provisionado no deploy, duram minutos.
/// Como a divulgação do certame espera a cópia, desistir cedo deixaria o certame inteiro fora do
/// ar com o ato já registrado.
/// </remarks>
public sealed class AcervoPublicoIndisponivelException(string message, Exception innerException)
    : Exception(message, innerException);
