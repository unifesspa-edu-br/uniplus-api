namespace Unifesspa.UniPlus.Authorization.Decisao;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Vocabulário fechado dos campos de contexto que uma permissão pode exigir
/// presentes (<c>EscopoContextoObrigatorio</c> do atributo de cada permissão, ADR-0151) e a
/// verificação de presença de cada um no <see cref="ResourceContext"/>.
/// </summary>
/// <remarks>
/// O conjunto é o mesmo que o gerador do catálogo aceita — mantê-los alinhados é
/// travado por teste. Um nome fora do vocabulário não é ignorado: a permissão
/// exigiu um contexto que esta versão do backend não sabe verificar, e a
/// verificação não realizada nunca deve virar acesso concedido.
/// </remarks>
public static class CamposDeContexto
{
    /// <summary>Campo do contexto que identifica a unidade dona do resource.</summary>
    public const string UnidadeProprietariaId = "unidadeProprietariaId";

    /// <summary>Campo do contexto que identifica a fase em que a banca atua.</summary>
    public const string FaseId = "faseId";

    /// <summary>Campo do contexto que identifica a etapa em que o ato acontece.</summary>
    public const string EtapaId = "etapaId";

    /// <summary>Campo do contexto que identifica a banca à qual a pessoa precisa estar designada.</summary>
    public const string BancaId = "bancaId";

    /// <summary>Campo do contexto que identifica a inscrição a que o acesso se refere.</summary>
    public const string InscricaoId = "inscricaoId";

    private static readonly FrozenDictionary<string, Func<ResourceContext, bool>> Presenca =
        new Dictionary<string, Func<ResourceContext, bool>>(StringComparer.Ordinal)
        {
            // Garantidos pela fábrica de ResourceContext (tipo obrigatório,
            // sensibilidade é enum não-anulável); exigi-los é redundante, mas
            // declarável — e continua verdadeiro.
            ["resourceTipo"] = static resource => !string.IsNullOrWhiteSpace(resource.ResourceTipo),
            ["sensibilidade"] = static _ => true,
            [UnidadeProprietariaId] = static resource => resource.UnidadeProprietariaId is not null,
            ["processoId"] = static resource => resource.ProcessoId is not null,
            ["chamadaId"] = static resource => resource.ChamadaId is not null,
            [FaseId] = static resource => resource.Banca is not null,
            [EtapaId] = static resource => resource.Banca?.EtapaId is not null,
            [BancaId] = static resource => resource.Banca is not null,
            [InscricaoId] = static resource => resource.InscricaoId is not null,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Nomes aceitos, que a conferência do catálogo na subida usa.</summary>
    public static IReadOnlySet<string> NomesConhecidos { get; } =
        Presenca.Keys.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// <see langword="true"/> quando algum campo exigido está ausente do resource
    /// — ou quando o nome exigido é desconhecido, que é a mesma coisa do ponto de
    /// vista de quem decide: não há como afirmar que o contexto foi satisfeito.
    /// </summary>
    internal static bool AlgumAusente(IReadOnlyList<string> exigidos, ResourceContext resource)
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
