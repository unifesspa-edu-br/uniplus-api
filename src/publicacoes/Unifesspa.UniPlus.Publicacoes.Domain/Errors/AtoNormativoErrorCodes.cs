namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

public static class AtoNormativoErrorCodes
{
    /// <summary>
    /// O tipo informado não tem versão do catálogo vigente na data de publicação
    /// do ato. Sem versão vigente não há de onde copiar <c>congela_configuracao</c>
    /// e <c>efeito_irreversivel</c>, então o registro é recusado.
    /// </summary>
    public const string TipoSemVersaoVigente = "AtoNormativo.TipoSemVersaoVigente";

    /// <summary>
    /// O par <c>{versao_invocada_id, versao_invocada_hash}</c> veio pela metade —
    /// um identificador sem hash, ou um hash sem identificador. É completo ou ausente.
    /// </summary>
    public const string VersaoInvocadaIncompleta = "AtoNormativo.VersaoInvocadaIncompleta";

    /// <summary>O identificador da URL não corresponde a nenhum ato registrado.</summary>
    public const string NaoEncontrado = "AtoNormativo.NaoEncontrado";

    /// <summary>
    /// A retificação referencia um <c>ato_retificado_id</c> que não corresponde a
    /// nenhum ato registrado. Não há linhagem para emendar.
    /// </summary>
    public const string AtoRetificadoNaoEncontrado = "AtoNormativo.AtoRetificadoNaoEncontrado";

    /// <summary>
    /// A classe de congelamento do retificador diverge da do retificado: um ato não
    /// congelante não emenda um congelante, nem o inverso (ADR-0103). É a
    /// <c>congela_configuracao</c> que protege a integridade da configuração
    /// publicada (RN08), não o rótulo do tipo.
    /// </summary>
    public const string ClasseDeCongelamentoDivergente = "AtoNormativo.ClasseDeCongelamentoDivergente";

    /// <summary>
    /// O ato que se tentou retificar já foi retificado por outro: a cadeia é linear
    /// e empilha na cabeça (ADR-0103). Uma nova retificação deve emendar a cabeça da
    /// cadeia, não uma raiz já retificada. A mensagem nomeia o ato que já a retificou.
    /// </summary>
    public const string RaizJaRetificada = "AtoNormativo.RaizJaRetificada";
}
