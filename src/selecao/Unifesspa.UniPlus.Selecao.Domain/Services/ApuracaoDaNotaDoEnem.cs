namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Apura a nota da etapa do ENEM: a média das cinco áreas ponderada pelos pesos do grupo de área
/// do curso, <c>Σ(nota × peso) ÷ Σ(pesos)</c>, sem arredondar. Função pura: só conhece os valores
/// que recebe, nunca o banco nem a configuração.
/// </summary>
/// <remarks>
/// O arredondamento é regra própria da classificação e acontece depois. A prova ausente entra com
/// a nota do lançamento, que a leitura do arquivo garante ser zero.
/// </remarks>
public static class ApuracaoDaNotaDoEnem
{
    /// <summary>
    /// A média ponderada de uma inscrição pelos pesos de um grupo. Pesos sem alguma das cinco áreas,
    /// ou que somam zero, violam o que o cadastro e o congelamento do quadro já recusam: por isso
    /// lançam, em vez de virar uma nota que parece válida.
    /// </summary>
    public static decimal Apurar(IReadOnlyList<ResultadoDaAreaNoRetorno> areas, IReadOnlyList<PesoDaArea> pesosDoGrupo)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(pesosDoGrupo);

        decimal somaDosPesos = 0m;
        decimal somaPonderada = 0m;
        foreach (string area in AreaDoEnem.Todas)
        {
            PesoDaArea? peso = pesosDoGrupo.FirstOrDefault(p => string.Equals(p.AreaCodigo, area, StringComparison.Ordinal));
            ResultadoDaAreaNoRetorno? lancamento = areas.FirstOrDefault(a => string.Equals(a.Area, area, StringComparison.Ordinal));
            if (peso is null || lancamento is null)
            {
                throw new InvalidOperationException($"A apuração exige o peso e a nota das cinco áreas do ENEM; falta a área {area}.");
            }

            somaDosPesos += peso.Peso;
            somaPonderada += lancamento.Nota * peso.Peso;
        }

        if (somaDosPesos <= 0m)
        {
            throw new InvalidOperationException("Os pesos do grupo de área somam zero: o quadro de pesos não pode chegar à apuração assim.");
        }

        return somaPonderada / somaDosPesos;
    }

    /// <summary>
    /// Uma avaliação por inscrição da edição e por oferta que ela concorre (1ª e, se houver, 2ª
    /// opção). Quem tem uma linha no arquivo recebe a nota do grupo de cada oferta; quem não tem,
    /// ou tem linhas em conflito, recebe a situação que explica a falta, sem nota.
    /// </summary>
    /// <param name="cruzamento">O que o arquivo disse de cada inscrição da edição.</param>
    /// <param name="inscritosDaEdicao">As inscrições da edição e as ofertas que cada uma concorre.</param>
    /// <param name="grupoPorOferta">O código do grupo de área de cada oferta do edital divulgado.</param>
    /// <param name="quadro">Os pesos das áreas de cada grupo, pelo código do grupo.</param>
    public static IReadOnlyList<AvaliacaoApurada> ApurarDoLote(
        ResultadoDoCruzamento cruzamento,
        IReadOnlyList<InscritoDaEdicao> inscritosDaEdicao,
        IReadOnlyDictionary<Guid, string> grupoPorOferta,
        IReadOnlyDictionary<string, IReadOnlyList<PesoDaArea>> quadro)
    {
        ArgumentNullException.ThrowIfNull(cruzamento);
        ArgumentNullException.ThrowIfNull(inscritosDaEdicao);
        ArgumentNullException.ThrowIfNull(grupoPorOferta);
        ArgumentNullException.ThrowIfNull(quadro);

        Dictionary<Guid, IReadOnlyList<Guid>> ofertasPorInscricao =
            inscritosDaEdicao.ToDictionary(static i => i.InscricaoId, static i => i.OfertasConcorridas);

        List<AvaliacaoApurada> avaliacoes = [];

        foreach (InscritoComNota inscrito in cruzamento.ComNota)
        {
            foreach (Guid oferta in OfertasDe(ofertasPorInscricao, inscrito.InscricaoId))
            {
                string grupo = GrupoDa(grupoPorOferta, oferta);
                decimal nota = Apurar(inscrito.Areas, PesosDo(quadro, grupo));
                avaliacoes.Add(new AvaliacaoApurada(inscrito.InscricaoId, oferta, grupo, SituacaoDaAvaliacao.Apurada, nota));
            }
        }

        AdicionarSemNota(avaliacoes, cruzamento.InscritosSemLinha, SituacaoDaAvaliacao.InscritoSemLinha, ofertasPorInscricao, grupoPorOferta);
        AdicionarSemNota(avaliacoes, [.. cruzamento.Conflitos.Select(static c => c.InscricaoId)], SituacaoDaAvaliacao.EmConflito, ofertasPorInscricao, grupoPorOferta);

        return avaliacoes;
    }

    private static void AdicionarSemNota(
        List<AvaliacaoApurada> avaliacoes,
        IReadOnlyList<Guid> inscricoes,
        SituacaoDaAvaliacao situacao,
        Dictionary<Guid, IReadOnlyList<Guid>> ofertasPorInscricao,
        IReadOnlyDictionary<Guid, string> grupoPorOferta)
    {
        foreach (Guid inscricaoId in inscricoes)
        {
            foreach (Guid oferta in OfertasDe(ofertasPorInscricao, inscricaoId))
            {
                avaliacoes.Add(new AvaliacaoApurada(inscricaoId, oferta, GrupoDa(grupoPorOferta, oferta), situacao, Nota: null));
            }
        }
    }

    private static IReadOnlyList<Guid> OfertasDe(Dictionary<Guid, IReadOnlyList<Guid>> ofertasPorInscricao, Guid inscricaoId) =>
        ofertasPorInscricao.TryGetValue(inscricaoId, out IReadOnlyList<Guid>? ofertas)
            ? ofertas
            : throw new InvalidOperationException("O cruzamento citou uma inscrição que não é da edição.");

    // A importação recusa o lote antes de chegar aqui quando uma inscrição concorre a oferta fora
    // do edital divulgado; o grupo de toda oferta do edital está no quadro porque a publicação exige.
    private static string GrupoDa(IReadOnlyDictionary<Guid, string> grupoPorOferta, Guid oferta) =>
        grupoPorOferta.TryGetValue(oferta, out string? grupo)
            ? grupo
            : throw new InvalidOperationException("A oferta concorrida não pertence ao edital divulgado.");

    private static IReadOnlyList<PesoDaArea> PesosDo(IReadOnlyDictionary<string, IReadOnlyList<PesoDaArea>> quadro, string grupo) =>
        quadro.TryGetValue(grupo, out IReadOnlyList<PesoDaArea>? pesos)
            ? pesos
            : throw new InvalidOperationException($"O grupo de área {grupo} não está no quadro de pesos do edital divulgado.");
}
