namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// A distribuição de uma oferta que a leitura adotou, ou <see langword="null"/> quando a recusou, com a posição dela no documento, a
/// posição de cada modalidade que o documento trouxe e as recusas que acumulou.
/// </summary>
internal sealed record DistribuicaoLida(
    ConfiguracaoDistribuicaoVagas? Distribuicao,
    int PosicaoNoDocumento,
    IReadOnlyList<int> PosicoesDasModalidades,
    IReadOnlyList<ParteRecusada> Recusadas);

/// <summary>As distribuições que a leitura adotou, na ordem do documento, e as partes que ela recusou.</summary>
internal sealed record DistribuicoesLidas(IReadOnlyList<DistribuicaoLida> Adotadas, IReadOnlyList<ParteRecusada> Recusadas)
{
    /// <summary>A origem da lista de distribuições e a das modalidades de cada uma, pela posição canônica da distribuição adotada.</summary>
    internal OrigemNoDocumento Origem()
    {
        Dictionary<string, IReadOnlyList<int>> posicoes = new(StringComparer.Ordinal)
        {
            [LeituraDasDistribuicoesDeVagas.Lista] = [.. Adotadas.Select(static adotada => adotada.PosicaoNoDocumento)],
        };
        for (int canonica = 0; canonica < Adotadas.Count; canonica++)
        {
            posicoes[$"{LeituraDasDistribuicoesDeVagas.Lista}[{canonica}].modalidades"] = Adotadas[canonica].PosicoesDasModalidades;
        }

        return new(posicoes, Recusadas);
    }
}

/// <summary>A cascata que a leitura adotou, ou <see langword="null"/> quando a recusou, a posição de cada destino e as partes recusadas.</summary>
internal sealed record CascataLida(
    ConfiguracaoCascataRemanejamento? Cascata,
    IReadOnlyList<int> PosicoesDosDestinos,
    IReadOnlyList<ParteRecusada> Recusadas)
{
    internal static CascataLida Ausente { get; } = new(null, [], []);

    internal OrigemNoDocumento Origem() => new(
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal) { [LeituraDaCascataDeRemanejamento.Lista] = PosicoesDosDestinos },
        Recusadas);
}

/// <summary>O Fator de Atenuação das Desigualdades de Acesso (FADA) que a leitura adotou, se o processo o aplica e as partes recusadas.</summary>
internal sealed record FadaLido(bool? Aplica, ConfiguracaoDoFada? Fada, IReadOnlyList<ParteRecusada> Recusadas)
{
    internal static FadaLido Ausente { get; } = new(null, null, []);

    internal OrigemNoDocumento Origem() => OrigemNoDocumento.SoComPartesRecusadas(Recusadas);
}

/// <summary>O atendimento especializado que a leitura adotou, a posição de cada item das três listas e as partes recusadas.</summary>
internal sealed record AtendimentoLido(
    AtendimentoEspecializado? Atendimento,
    IReadOnlyList<int> PosicoesDasCondicoes,
    IReadOnlyList<int> PosicoesDosRecursos,
    IReadOnlyList<int> PosicoesDosTiposDeDeficiencia,
    IReadOnlyList<ParteRecusada> Recusadas)
{
    internal static AtendimentoLido Ausente { get; } = new(null, [], [], [], []);

    internal OrigemNoDocumento Origem() => new(
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
        {
            [LeituraDoAtendimento.ListaDasCondicoes] = PosicoesDasCondicoes,
            [LeituraDoAtendimento.ListaDosRecursos] = PosicoesDosRecursos,
            [LeituraDoAtendimento.ListaDosTiposDeDeficiencia] = PosicoesDosTiposDeDeficiencia,
        },
        Recusadas);
}

/// <summary>O número de opções de curso que a leitura adotou, ou <see langword="null"/> quando ausente ou recusado, e as partes recusadas.</summary>
internal sealed record OpcoesDeCursoLidas(int? Numero, IReadOnlyList<ParteRecusada> Recusadas)
{
    internal OrigemNoDocumento Origem() => OrigemNoDocumento.SoComPartesRecusadas(Recusadas);
}
