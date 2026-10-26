namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;
using DTOs.NotasDoEnem;

using Kernel.Results;

/// <summary>
/// Os cursos que cada inscrito escolheu, com o nome da oferta congelado na versão da inscrição
/// (curso e grau, unidade e turnos, como o portal os mostra). Cada versão é reidratada uma vez.
/// </summary>
public static class NomesDasOfertasDoLote
{
    private static readonly Dictionary<string, string> RotuloDoTurno = new(StringComparer.Ordinal)
    {
        ["MATUTINO"] = "Matutino",
        ["VESPERTINO"] = "Vespertino",
        ["NOTURNO"] = "Noturno",
    };

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>>> ResolverAsync(
        Guid processoSeletivoId,
        IReadOnlyDictionary<Guid, InscritoDoLote> inscritos,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscritos);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Dictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>> resultado = [];
        if (inscritos.Count == 0)
        {
            return resultado;
        }

        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertasPorVersao = await ContextoDaVersaoFixadaResolver
            .OfertasDasVersoesAsync(
                inscritos.Values.Select(e => (processoSeletivoId, e.VersaoFormularioId)),
                processoSeletivoRepository,
                registroCodecs,
                cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, Dictionary<Guid, string>> nomesPorVersao = ofertasPorVersao.ToDictionary(
            static v => v.Key,
            static v => v.Value.ToDictionary(static o => o.OfertaCursoOrigemId, Nome));

        foreach ((Guid inscricaoId, InscritoDoLote inscrito) in inscritos)
        {
            if (!nomesPorVersao.TryGetValue(inscrito.VersaoFormularioId, out Dictionary<Guid, string>? nomes))
            {
                continue;
            }

            IReadOnlyList<OpcaoDeCursoDoLoteDto> opcoes = Opcoes(inscrito, nomes);
            resultado[inscricaoId] = opcoes;
        }

        return resultado;
    }

    /// <summary>As opções que a versão nomeia, cada uma com a ordem em que o inscrito a escolheu.</summary>
    internal static List<OpcaoDeCursoDoLoteDto> Opcoes(InscritoDoLote inscrito, IReadOnlyDictionary<Guid, string> nomes)
    {
        List<OpcaoDeCursoDoLoteDto> opcoes = [];
        Guid?[] escolhidas = [inscrito.PrimeiraOpcao, inscrito.SegundaOpcao];
        for (int posicao = 0; posicao < escolhidas.Length; posicao++)
        {
            if (escolhidas[posicao] is { } id && nomes.TryGetValue(id, out string? nome))
            {
                opcoes.Add(new OpcaoDeCursoDoLoteDto(posicao + 1, nome));
            }
        }

        return opcoes;
    }

    private static string Nome(OfertaDeCursoCertameDto oferta) =>
        string.Join(
            " — ",
            $"{oferta.CursoNome} ({oferta.CursoGrau})",
            oferta.UnidadeOfertanteNome,
            string.Join(" e ", oferta.Turnos.Select(static t => RotuloDoTurno.GetValueOrDefault(t, t))));
}
