namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

/// <summary>
/// Regras da configuração publicada de uma versão, usadas na avaliação da inscrição.
/// A pré-visualização também usa este tipo para avaliar a configuração viva do processo.
/// </summary>
public sealed record RegrasDeComprovacaoDaVersao
{
    private RegrasDeComprovacaoDaVersao(IReadOnlyList<RegraDeComprovacao> regrasSemGrupo)
    {
        RegrasSemGrupo = regrasSemGrupo;
    }

    /// <summary>Regras sem grupo que as contenha.</summary>
    public IReadOnlyList<RegraDeComprovacao> RegrasSemGrupo { get; }

    /// <summary>
    /// Constrói as regras sem grupo que as contenha a partir da configuração reidratada da versão publicada.
    /// </summary>
    public static RegrasDeComprovacaoDaVersao DeGrafoReidratado(IReadOnlyList<RegraDeComprovacao> regrasSemGrupo)
    {
        ArgumentNullException.ThrowIfNull(regrasSemGrupo);
        return new RegrasDeComprovacaoDaVersao([.. regrasSemGrupo]);
    }

    /// <summary>
    /// As regras da configuração viva do processo — o rascunho —, para a
    /// pré-visualização: mostra o que a configuração em edição exigiria, não o que foi publicado.
    /// </summary>
    public static RegrasDeComprovacaoDaVersao DaConfiguracaoViva(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        return new RegrasDeComprovacaoDaVersao([.. processo.RegrasDeComprovacaoSemGrupo]);
    }
}
