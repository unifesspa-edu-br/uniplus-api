namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// Aponta as chaves que se repetem dentro de um mesmo <see cref="ConjuntoDaChaveUnica"/>. A primeira parte com a chave, na
/// ordem do documento enviado, é a dona dela; cada parte seguinte com a mesma chave é apontada no ponteiro da própria chave.
/// A chave ausente não é conferida aqui: é o esquema que a exige.
/// </summary>
internal static class ConferenciaDasChaves
{
    private const string NomeDaPropriedade = "chave";

    public static IReadOnlyList<FieldError> Repetidas(IReadOnlyList<NoDoDocumento> nos) =>
    [
        .. nos.Where(static no => no.Forma.Parte is not null && no.Valor is JsonObject)
            .Select(static parte => (Conjunto: ConjuntoDaChaveUnica.De(parte), Chave: ChaveEscrita(parte), parte.Ponteiro))
            .Where(static parte => parte.Chave is not null)
            .GroupBy(static parte => (parte.Conjunto, parte.Chave))
            .Where(static grupo => grupo.Count() > 1)
            .SelectMany(static grupo => grupo.Skip(1))
            .Select(static parte => CausasDaForma.ChaveRepetida(PonteiroJson.Acrescentar(parte.Ponteiro, NomeDaPropriedade))),
    ];

    private static string? ChaveEscrita(NoDoDocumento parte) =>
        ((JsonObject)parte.Valor)[NomeDaPropriedade] is JsonValue valor && valor.TryGetValue(out string? chave) ? chave : null;
}
