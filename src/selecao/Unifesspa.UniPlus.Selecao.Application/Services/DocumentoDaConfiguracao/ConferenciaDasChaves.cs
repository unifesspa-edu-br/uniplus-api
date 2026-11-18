namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// Aponta as chaves que se repetem dentro de um mesmo <see cref="ConjuntoDaChaveUnica"/>. A primeira parte com a chave, na
/// ordem do documento enviado, é a dona dela; cada parte seguinte com a mesma chave é apontada no ponteiro da própria chave.
/// Na distribuição de vagas, a chave é o código da oferta de curso. A chave ausente não é conferida aqui: é o esquema que a exige.
/// </summary>
internal static class ConferenciaDasChaves
{
    private const string NomeDaPropriedade = "chave";

    public static IReadOnlyList<FieldError> Repetidas(IReadOnlyList<NoDoDocumento> nos) =>
        [.. ChavesLocaisRepetidas(nos), .. OfertasRepetidas(nos)];

    private static IEnumerable<FieldError> ChavesLocaisRepetidas(IReadOnlyList<NoDoDocumento> nos) =>
    [
        .. nos.Where(static no => no.Forma.Parte is not null && no.Valor is JsonObject)
            .Select(static parte => (Conjunto: ConjuntoDaChaveUnica.De(parte), Chave: ChaveEscrita(parte), parte.Ponteiro))
            .Where(static parte => parte.Chave is not null)
            .GroupBy(static parte => (parte.Conjunto, parte.Chave))
            .Where(static grupo => grupo.Count() > 1)
            .SelectMany(static grupo => grupo.Skip(1))
            .Select(static parte => CausasDaForma.ChaveRepetida(PonteiroJson.Acrescentar(parte.Ponteiro, NomeDaPropriedade))),
    ];

    private static IEnumerable<FieldError> OfertasRepetidas(IReadOnlyList<NoDoDocumento> nos) =>
        nos.Where(static no => PonteiroJson.Segmentos(no.Ponteiro) is ["distribuicaoVagas", _, "ofertaCursoCodigo"])
            .Select(static no => (no.Ponteiro, Codigo: no.Valor is JsonValue valor && valor.TryGetValue(out string? codigo) ? codigo : null))
            .Where(static oferta => oferta.Codigo is not null)
            .GroupBy(static oferta => oferta.Codigo, StringComparer.Ordinal)
            .SelectMany(static grupo => grupo.Skip(1))
            .Select(static oferta => CausasDaForma.OfertaCursoRepetida(oferta.Ponteiro));

    private static string? ChaveEscrita(NoDoDocumento parte) =>
        ((JsonObject)parte.Valor)[NomeDaPropriedade] is JsonValue valor && valor.TryGetValue(out string? chave) ? chave : null;
}
