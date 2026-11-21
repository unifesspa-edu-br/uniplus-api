namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O tipo de banca que exige duas ou mais bancas tem ao menos duas no lugar (a fase ou a etapa) e, na fase, em cada grupo de
/// candidatos atendidos. A exigência vem da cópia do tipo que cada banca guarda, e a contagem inclui toda banca do mesmo tipo,
/// traga ela a exigência ou não. Quando o lugar já tem só uma banca do tipo, a falta é do lugar e não se repete no grupo.
/// </summary>
internal static class MinimoDeBancasPorTipo
{
    /// <summary>Onde falta banca: no lugar inteiro ou num grupo de candidatos atendidos da fase.</summary>
    internal enum EscopoDaFalta
    {
        Lugar,
        Grupo,
    }

    /// <summary>A banca que ficou sozinha e o escopo em que falta outra do mesmo tipo.</summary>
    internal sealed record Falta(BancaDeclarada Banca, EscopoDaFalta Escopo);

    /// <summary>
    /// As faltas das bancas do lugar, por tipo na ordem em que aparece. Só a fase tem grupos; a etapa passa a lista vazia e o
    /// lugar é o único escopo.
    /// </summary>
    internal static IReadOnlyList<Falta> Faltas(IReadOnlyList<BancaDeclarada> bancas, IReadOnlyList<GrupoDeCandidatosAtendidos> gruposDaFase)
    {
        ArgumentNullException.ThrowIfNull(bancas);
        ArgumentNullException.ThrowIfNull(gruposDaFase);

        return [.. bancas
            .GroupBy(static banca => banca.Tipo.Codigo)
            .SelectMany(doTipo => FaltasDoTipo([.. doTipo], bancas, gruposDaFase))];
    }

    internal static DomainError Recusa(Falta falta, string onde)
    {
        ArgumentNullException.ThrowIfNull(falta);

        string citacao = falta.Banca.Citacao;

        return new(
            BancaErrorCodes.TipoExigeMaisDeUmaBanca,
            falta.Escopo == EscopoDaFalta.Lugar
                ? $"A {onde} prevê só a banca '{citacao}' desse tipo, e este tipo de banca exige duas ou mais bancas. Acrescente outra banca do mesmo tipo."
                : $"Na {onde}, a banca '{citacao}' é a única do tipo no seu grupo de candidatos atendidos, e este tipo de banca exige duas ou mais bancas em cada grupo. Acrescente outra banca do mesmo tipo com as mesmas categorias de documento.");
    }

    private static IEnumerable<Falta> FaltasDoTipo(
        IReadOnlyList<BancaDeclarada> doTipo, IReadOnlyList<BancaDeclarada> bancas, IReadOnlyList<GrupoDeCandidatosAtendidos> gruposDaFase)
    {
        if (UnicaQueExige(doTipo) is { } soUma)
        {
            return [new Falta(soUma, EscopoDaFalta.Lugar)];
        }

        return gruposDaFase
            .Where(grupo => grupo.Tipo == doTipo[0].Tipo.Codigo)
            .Select(grupo => UnicaQueExige([.. grupo.Posicoes.Select(posicao => bancas[posicao])]))
            .OfType<BancaDeclarada>()
            .Select(static banca => new Falta(banca, EscopoDaFalta.Grupo));
    }

    private static BancaDeclarada? UnicaQueExige(IReadOnlyList<BancaDeclarada> conjunto) =>
        conjunto is [{ Tipo.ExigeDuasOuMaisBancas: true } unica] ? unica : null;
}
