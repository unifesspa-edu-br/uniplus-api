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
        IReadOnlyDictionary<Guid, OfertasEscolhidasDaInscricao> escolhidas,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(escolhidas);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Dictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>> resultado = [];
        if (escolhidas.Count == 0)
        {
            return resultado;
        }

        Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>> ofertasPorVersao = await ContextoDaVersaoFixadaResolver
            .OfertasDasVersoesAsync(
                escolhidas.Values.Select(e => (processoSeletivoId, e.VersaoFormularioId)),
                processoSeletivoRepository,
                registroCodecs,
                cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, Dictionary<Guid, string>> nomesPorVersao = ofertasPorVersao.ToDictionary(
            static v => v.Key,
            static v => v.Value.ToDictionary(static o => o.OfertaCursoOrigemId, Nome));

        foreach ((Guid inscricaoId, OfertasEscolhidasDaInscricao ofertas) in escolhidas)
        {
            if (!nomesPorVersao.TryGetValue(ofertas.VersaoFormularioId, out Dictionary<Guid, string>? nomes))
            {
                continue;
            }

            List<OpcaoDeCursoDoLoteDto> opcoes = [];
            foreach (Guid? escolhida in new[] { ofertas.Primeira, ofertas.Segunda })
            {
                if (escolhida is not { } id || !nomes.TryGetValue(id, out string? nome))
                {
                    break;
                }

                opcoes.Add(new OpcaoDeCursoDoLoteDto(opcoes.Count + 1, nome));
            }

            resultado[inscricaoId] = opcoes;
        }

        return resultado;
    }

    private static string Nome(OfertaDeCursoCertameDto oferta) =>
        string.Join(
            " — ",
            $"{oferta.CursoNome} ({oferta.CursoGrau})",
            oferta.UnidadeOfertanteNome,
            string.Join(" e ", oferta.Turnos.Select(static t => RotuloDoTurno.GetValueOrDefault(t, t))));
}
