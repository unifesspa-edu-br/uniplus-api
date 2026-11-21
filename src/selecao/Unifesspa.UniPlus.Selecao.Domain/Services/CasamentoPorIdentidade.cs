namespace Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Confronta o item de catálogo que a regra cita por código com as contrapartes congeladas no
/// processo: a identidade da origem decide se a regra casa, e o código só explica ao humano
/// o que houve (ADR-0129).
/// </summary>
internal static class CasamentoPorIdentidade
{
    /// <summary>
    /// Confronta o código citado pela regra com as contrapartes congeladas, decidindo por
    /// identidade e reservando o código apenas para explicar o que houve. Código ausente do mapa
    /// não designa item vivo algum: não casa, e não há identidade sobre a qual diagnosticar
    /// reatribuição.
    /// </summary>
    /// <param name="identidadeViva">Código para identificador, do cadastro vivo.</param>
    /// <param name="codigoExigido">O que a regra cita.</param>
    /// <param name="queSatisfazem">
    /// Contrapartes que, casando, aprovam a regra. Nem sempre são todas: a exigência
    /// documental só satisfaz quando cobre a modalidade incondicionalmente.
    /// </param>
    /// <param name="todasAsContrapartes">
    /// Universo consultado apenas para o diagnóstico de reatribuição — uma exigência que
    /// carrega o código mas não satisfaz ainda assim explica por que o editor a vê na tela.
    /// </param>
    /// <param name="origemDe">Identidade congelada da contraparte.</param>
    /// <param name="codigoDe">Código congelado da contraparte.</param>
    internal static Casamento Casar<T>(
        IReadOnlyDictionary<string, Guid> identidadeViva,
        string? codigoExigido,
        IEnumerable<T> queSatisfazem,
        IEnumerable<T> todasAsContrapartes,
        Func<T, Guid> origemDe,
        Func<T, string> codigoDe)
    {
        if (!identidadeViva.TryGetValue(codigoExigido ?? string.Empty, out Guid identidadeExigida))
        {
            return Casamento.NaoCasa;
        }

        if (queSatisfazem.Any(c => origemDe(c) == identidadeExigida))
        {
            return Casamento.Casa;
        }

        return CarregaOCodigoDeOutroItem(todasAsContrapartes, codigoExigido, identidadeExigida, origemDe, codigoDe)
            ? Casamento.CodigoReatribuido
            : Casamento.NaoCasa;
    }

    /// <summary>
    /// Contraparte sem identidade não prova reatribuição: ela apenas não diz a que item pertence,
    /// e afirmar reatribuição ali seria um fato que o dado não sustenta.
    /// </summary>
    internal static bool CarregaOCodigoDeOutroItem<T>(
        IEnumerable<T> contrapartes,
        string? codigo,
        Guid identidadeExigida,
        Func<T, Guid> origemDe,
        Func<T, string> codigoDe) =>
        contrapartes.Any(c =>
            origemDe(c) != Guid.Empty
            && origemDe(c) != identidadeExigida
            && string.Equals(codigoDe(c), codigo, StringComparison.Ordinal));

    /// <summary>
    /// Separa os códigos exigidos entre os que nenhuma contraparte cobre e os que só
    /// aparecem reatribuídos a outro item.
    /// </summary>
    internal static (List<string> Ausentes, List<string> Reatribuidos) Classificar<T>(
        IEnumerable<string> codigos,
        IReadOnlyDictionary<string, Guid> identidadeViva,
        IReadOnlyCollection<T> contrapartes,
        Func<T, Guid> origemDe,
        Func<T, string> codigoDe)
    {
        List<(string Codigo, Casamento Casamento)> casamentos =
            [.. codigos.Select(c => (c, Casar(identidadeViva, c, contrapartes, contrapartes, origemDe, codigoDe)))];

        return (
            [.. casamentos.Where(static c => c.Casamento == Casamento.NaoCasa).Select(static c => c.Codigo)],
            [.. casamentos.Where(static c => c.Casamento == Casamento.CodigoReatribuido).Select(static c => c.Codigo)]);
    }
}
