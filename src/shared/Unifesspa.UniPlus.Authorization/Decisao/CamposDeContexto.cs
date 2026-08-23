namespace Unifesspa.UniPlus.Authorization.Decisao;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Vocabulário fechado dos campos de contexto que uma permissão pode exigir
/// presentes (<c>context_scope</c> no catálogo declarativo, ADR-0080) e a
/// verificação de presença de cada um no <see cref="ResourceContext"/>.
/// </summary>
/// <remarks>
/// O conjunto é o mesmo que o gerador do catálogo aceita — mantê-los alinhados é
/// travado por teste. Um nome fora do vocabulário não é ignorado: a permissão
/// exigiu um contexto que esta versão do backend não sabe verificar, e a
/// verificação não realizada nunca deve virar acesso concedido.
/// </remarks>
internal static class CamposDeContexto
{
    private static readonly FrozenDictionary<string, Func<ResourceContext, bool>> Presenca =
        new Dictionary<string, Func<ResourceContext, bool>>(StringComparer.Ordinal)
        {
            // Garantidos pela fábrica de ResourceContext (tipo obrigatório,
            // sensibilidade é enum não-anulável); exigi-los é redundante, mas
            // declarável — e continua verdadeiro.
            ["recursoTipo"] = static resource => !string.IsNullOrWhiteSpace(resource.RecursoTipo),
            ["sensibilidade"] = static _ => true,
            ["unidadeProprietariaId"] = static resource => resource.UnidadeProprietariaId is not null,
            ["processoId"] = static resource => resource.ProcessoId is not null,
            ["chamadaId"] = static resource => resource.ChamadaId is not null,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Nomes aceitos, para o teste que os amarra ao catálogo declarativo.</summary>
    public static IReadOnlySet<string> NomesConhecidos { get; } =
        Presenca.Keys.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// <see langword="true"/> quando algum campo exigido está ausente do recurso
    /// — ou quando o nome exigido é desconhecido, que é a mesma coisa do ponto de
    /// vista de quem decide: não há como afirmar que o contexto foi satisfeito.
    /// </summary>
    public static bool AlgumAusente(IReadOnlyList<string> exigidos, ResourceContext resource)
    {
        foreach (string nome in exigidos)
        {
            if (!Presenca.TryGetValue(nome, out Func<ResourceContext, bool>? estaPresente)
                || !estaPresente(resource))
            {
                return true;
            }
        }

        return false;
    }
}
