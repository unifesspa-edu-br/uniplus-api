namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cruza as linhas de um arquivo de retorno com as inscrições da edição pelo CPF. Função pura: não
/// conhece o leiaute nem o banco, só as linhas já lidas e os índices cegos dos CPFs.
/// </summary>
public static class CruzamentoDasNotasComInscritos
{
    /// <param name="linhas">As linhas lidas do arquivo.</param>
    /// <param name="indicePorCpf">O índice cego de cada CPF distinto do arquivo.</param>
    /// <param name="inscritosDaEdicao">As inscrições enviadas que escolheram a edição.</param>
    public static ResultadoDoCruzamento Cruzar(
        IReadOnlyList<LinhaDeRetorno> linhas,
        IReadOnlyDictionary<string, IndiceCegoDoCpf> indicePorCpf,
        IReadOnlyList<InscritoDaEdicao> inscritosDaEdicao)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        ArgumentNullException.ThrowIfNull(indicePorCpf);
        ArgumentNullException.ThrowIfNull(inscritosDaEdicao);

        Dictionary<IndiceCegoDoCpf, Guid> inscricaoPorIndice = inscritosDaEdicao.ToDictionary(static i => i.Indice, static i => i.InscricaoId);

        List<InscritoComNota> comNota = [];
        List<ConflitoDeLinhas> conflitos = [];
        List<int> linhasSemInscrito = [];
        HashSet<Guid> comLinha = [];

        foreach (IGrouping<string, LinhaDeRetorno> grupo in linhas.GroupBy(static l => l.Cpf, StringComparer.Ordinal))
        {
            if (!inscricaoPorIndice.TryGetValue(indicePorCpf[grupo.Key], out Guid inscricaoId))
            {
                linhasSemInscrito.AddRange(grupo.Select(static l => l.NumeroDaLinha));
                continue;
            }

            comLinha.Add(inscricaoId);
            LinhaDeRetorno[] linhasDoInscrito = [.. grupo];

            if (linhasDoInscrito.Select(static l => l.ChaveDeIgualdade).Distinct(StringComparer.Ordinal).Count() == 1)
            {
                LinhaDeRetorno linha = linhasDoInscrito[0];
                comNota.Add(new InscritoComNota(inscricaoId, linha.NumeroDeInscricaoNoEnem, linha.Areas));
            }
            else
            {
                conflitos.Add(new ConflitoDeLinhas(inscricaoId, [.. linhasDoInscrito.Select(static l => l.NumeroDaLinha).Order()]));
            }
        }

        return new ResultadoDoCruzamento(
            comNota,
            [.. inscritosDaEdicao.Select(static i => i.InscricaoId).Where(id => !comLinha.Contains(id)).Order()],
            [.. linhasSemInscrito.Order()],
            [.. conflitos.OrderBy(static c => c.NumerosDasLinhas[0])]);
    }
}
