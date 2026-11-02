namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Interfaces;

using DTOs;
using DTOs.NotasDoEnem;

/// <summary>
/// As ofertas que cada inscrito escolheu, nomeadas pela versão do formulário a que a inscrição está presa (curso e grau,
/// unidade e turnos, como o portal as mostra). É a única montagem do nome e da ordem, usada pelo lote de notas do ENEM e
/// pelas duas consultas da inscrição.
/// </summary>
public static class OfertasEscolhidas
{
    private static readonly Dictionary<string, string> RotuloDoTurno = new(StringComparer.Ordinal)
    {
        ["MATUTINO"] = "Matutino",
        ["VESPERTINO"] = "Vespertino",
        ["NOTURNO"] = "Noturno",
    };

    /// <summary>
    /// As ofertas escolhidas, na ordem da escolha, a partir de 1 e sem lacuna. A oferta que a versão não nomeia sai com o
    /// nome nulo: a escolha nunca é descartada.
    /// </summary>
    public static IReadOnlyList<OfertaEscolhidaDto> Montar(
        Guid? primeiraOpcao, Guid? segundaOpcao, IReadOnlyDictionary<Guid, string> nomes)
    {
        ArgumentNullException.ThrowIfNull(nomes);

        return
        [
            .. new[] { primeiraOpcao, segundaOpcao }
                .OfType<Guid>()
                .Select((id, indice) => new OfertaEscolhidaDto(id, nomes.GetValueOrDefault(id), indice + 1)),
        ];
    }

    /// <summary>O nome de cada oferta da versão, pelo id da oferta de origem.</summary>
    public static Dictionary<Guid, string> NomesDe(IEnumerable<OfertaDeCursoCertameDto> ofertas)
    {
        ArgumentNullException.ThrowIfNull(ofertas);

        return ofertas.ToDictionary(static o => o.OfertaCursoOrigemId, Nome);
    }

    /// <summary>As ofertas de cada inscrito do lote, pelo nome congelado na versão dele. Cada versão é reidratada uma vez.</summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<OfertaEscolhidaDto>>> ResolverAsync(
        Guid processoSeletivoId,
        IReadOnlyDictionary<Guid, InscritoDoLote> inscritos,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscritos);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Dictionary<Guid, IReadOnlyList<OfertaEscolhidaDto>> resultado = [];
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
            static v => v.Key, static v => NomesDe(v.Value));

        foreach ((Guid inscricaoId, InscritoDoLote inscrito) in inscritos)
        {
            IReadOnlyDictionary<Guid, string> nomes = nomesPorVersao.TryGetValue(inscrito.VersaoFormularioId, out Dictionary<Guid, string>? daVersao)
                ? daVersao
                : new Dictionary<Guid, string>();
            resultado[inscricaoId] = Montar(inscrito.PrimeiraOpcao, inscrito.SegundaOpcao, nomes);
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
