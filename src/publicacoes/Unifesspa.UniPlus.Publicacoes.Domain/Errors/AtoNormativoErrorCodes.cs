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
}
