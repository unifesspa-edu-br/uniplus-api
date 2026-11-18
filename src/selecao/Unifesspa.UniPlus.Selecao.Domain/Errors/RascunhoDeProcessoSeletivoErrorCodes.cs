namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro do rascunho do processo seletivo. O rascunho que não existe responde com o código do processo que
/// não existe, <c>ProcessoSeletivo.NaoEncontrado</c>: o rascunho é o processo antes da primeira publicação.
/// </summary>
public static class RascunhoDeProcessoSeletivoErrorCodes
{
    /// <summary>O rascunho já foi publicado e virou processo; responde 409.</summary>
    public const string JaPublicado = "RascunhoDeProcessoSeletivo.JaPublicado";

    /// <summary>O documento não diz qual unidade administradora responde pelo processo; responde 422.</summary>
    public const string UnidadeAdministradoraAusente = "RascunhoDeProcessoSeletivo.UnidadeAdministradoraAusente";

    /// <summary>A pessoa não tem a permissão na unidade que o documento nomeia; responde 403.</summary>
    public const string UnidadeForaDaPermissao = "RascunhoDeProcessoSeletivo.UnidadeForaDaPermissao";

    /// <summary>O documento do rascunho veio sem conteúdo.</summary>
    public const string ConteudoDoDocumentoAusente = "RascunhoDeProcessoSeletivo.ConteudoDoDocumentoAusente";

    /// <summary>O nome do processo no documento passa do tamanho que a listagem comporta.</summary>
    public const string NomeAcimaDoLimite = "RascunhoDeProcessoSeletivo.NomeAcimaDoLimite";
}
