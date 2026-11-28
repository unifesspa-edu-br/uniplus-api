namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using CampoDoMembro = (string FatoCodigo, bool TemExibicao, Unifesspa.UniPlus.Regras.Formularios.Obrigatoriedade Obrigatoriedade, System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Formularios.RestricaoValor> Restricoes);

/// <summary>
/// A forma de um grupo repetível lida da entrada, a mesma no processo e no modelo: a forma do grupo e, quando ele inclui
/// o candidato, a identificação da ocorrência dele. Confere o grupo mesmo quando alguma parte dele não pôde ser lida;
/// a regra em si é de <see cref="FormaDoGrupo"/>.
/// </summary>
public static class FormaDoGrupoNaEntrada
{
    /// <summary>
    /// As recusas da forma do grupo e depois as do candidato como membro, no caminho a partir do grupo. A obrigatoriedade
    /// recusada na leitura vale <c>Nunca</c> e as ocorrências recusadas valem as lidas, para o resto ser conferido mesmo assim.
    /// </summary>
    /// <param name="campos">
    /// Um por campo da entrada, na mesma ordem. O campo que não se leu é nulo e conta como conforme: a recusa dele já existe, e
    /// aqui só se acusa o que falta ao grupo.
    /// </param>
    public static List<FieldError> Conferir(
        GrupoColetadoInput entrada,
        OcorrenciasLidasDoGrupo ocorrencias,
        PredicadoDnf? exibicao,
        Obrigatoriedade? obrigatoriedade,
        IReadOnlyList<CampoDoMembro?> campos)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(ocorrencias);
        ArgumentNullException.ThrowIfNull(campos);

        return
        [
            .. FormaDoGrupoLida(entrada, ocorrencias, ExibicaoEObrigatoriedade.ParaConferirAForma(exibicao, obrigatoriedade)),
            .. ocorrencias.ConferirCandidatoComoMembro(entrada.Subitens is null ? null : [.. campos.Select((campo, indice) => campo ?? Conforme(entrada.Subitens[indice]))]),
        ];
    }

    private static List<FieldError> FormaDoGrupoLida(GrupoColetadoInput entrada, OcorrenciasLidasDoGrupo ocorrencias, ExibicaoEObrigatoriedade regras) =>
        FormaDoGrupo.Conferir(
            entrada.Codigo,
            entrada.Ordem,
            entrada.Rotulo,
            ocorrencias.ParaAConferenciaDaForma,
            entrada.Subitens is null ? null : [.. entrada.Subitens.Select(static s => (s?.FatoCodigo, s?.PaginaCodigo, s?.Regras?.Impedimento is not null))],
            regras);

    private static CampoDoMembro Conforme(FatoColetadoInput? subitem) =>
        (subitem?.FatoCodigo ?? string.Empty, false, Obrigatoriedade.Sempre, []);
}
