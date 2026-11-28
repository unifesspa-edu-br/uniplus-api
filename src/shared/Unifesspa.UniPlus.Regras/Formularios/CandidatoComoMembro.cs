namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

using CampoDoMembro = (string FatoCodigo, bool TemExibicao, Unifesspa.UniPlus.Regras.Formularios.Obrigatoriedade Obrigatoriedade, System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Formularios.RestricaoValor> Restricoes);

/// <summary>
/// O grupo repetível que inclui o próprio candidato entre os membros, como a composição familiar
/// (UNI-REQ-0146): a ocorrência do candidato é a de parentesco <see cref="ProprioCandidato"/>, e
/// as exigências por membro a alcançam como a qualquer outra.
/// </summary>
public static class CandidatoComoMembro
{
    public const string FatoParentesco = "PARENTESCO";
    public const string ProprioCandidato = "PROPRIO_CANDIDATO";

    /// <summary>
    /// O grupo que inclui o candidato tem ao menos uma ocorrência e um campo de parentesco sempre
    /// exibido e obrigatório; se o campo restringe as opções, o próprio candidato está entre as que
    /// valem sempre — opções formadas pelas respostas anteriores não o garantem. Sem isso, nenhuma
    /// resposta identificaria a ocorrência do candidato. O mínimo não declarado já tem a recusa da
    /// leitura e não é conferido aqui; o parentesco é conferido mesmo assim, menos quando os campos faltam (<see langword="null"/>),
    /// porque a recusa deles já existe e o parentesco depende deles.
    /// </summary>
    public static List<FieldError> Conferir(int? minimo, IReadOnlyList<CampoDoMembro>? subitens)
    {
        if (subitens is null)
        {
            return [.. RecusasDoMinimo(minimo)];
        }

        int indice = subitens.ToList().FindIndex(static s => string.Equals(s.FatoCodigo, FatoParentesco, StringComparison.Ordinal));
        return [.. RecusasDoMinimo(minimo), .. indice < 0 ? RecusasDoParentescoAusente() : RecusasDoParentesco(indice, subitens[indice])];
    }

    private static IEnumerable<FieldError> RecusasDoMinimo(int? minimo) =>
        minimo is < 1 ? [Recusa($"{GrupoColetadoInput.CaminhoDasOcorrencias}.minimo", "O grupo que inclui o candidato tem ao menos uma ocorrência: a do próprio candidato.")] : [];

    private static IEnumerable<FieldError> RecusasDoParentescoAusente() =>
        [Recusa("subitens", $"O grupo que inclui o candidato pede o parentesco ({FatoParentesco}) em cada ocorrência.")];

    private static IEnumerable<FieldError> RecusasDoParentesco(int indice, CampoDoMembro parentesco) =>
    [
        .. RecusasDaExibicaoEDaObrigatoriedade(indice, parentesco),
        .. RecusasDasOpcoes(indice, parentesco.Restricoes),
    ];

    private static IEnumerable<FieldError> RecusasDaExibicaoEDaObrigatoriedade(int indice, CampoDoMembro parentesco) =>
        parentesco.TemExibicao || parentesco.Obrigatoriedade.Tipo != TipoObrigatoriedade.Sempre
            ? [Recusa($"subitens[{indice}]", "O parentesco do grupo que inclui o candidato é sempre exibido e obrigatório.")]
            : [];

    private static IEnumerable<FieldError> RecusasDasOpcoes(int indice, IReadOnlyList<RestricaoValor> restricoes) =>
        restricoes.Any(static r => !AdmiteSempreOProprioCandidato(r))
            ? [Recusa(
                $"subitens[{indice}].{FatoColetadoInput.CaminhoDasRegras}.restricoes",
                $"As opções de parentesco do grupo que inclui o candidato admitem sempre o próprio candidato ({ProprioCandidato}).")]
            : [];

    private static bool AdmiteSempreOProprioCandidato(RestricaoValor restricao) => restricao switch
    {
        OpcoesDasRespostas => false,
        OpcoesPermitidas permitidas => permitidas.Entradas.Any(static e => e.Quando is null && e.Valores.Contains(ProprioCandidato)),
        _ => true,
    };

    private static FieldError Recusa(string campo, string mensagem) =>
        new(campo, new DomainError(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto, mensagem));

    /// <summary>A lista respondida tem exatamente uma ocorrência do próprio candidato.</summary>
    public static bool TemUmaOcorrenciaDoCandidato(IEnumerable<OcorrenciaRespondida> ocorrencias)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);

        return ocorrencias.Count(static o => EhDoCandidato(o.Respostas)) == 1;
    }

    /// <summary>A ocorrência é a do próprio candidato: o parentesco dela é <see cref="ProprioCandidato"/>.</summary>
    public static bool EhDoCandidato(IReadOnlyDictionary<string, JsonElement> respostas)
    {
        ArgumentNullException.ThrowIfNull(respostas);

        return respostas.TryGetValue(FatoParentesco, out JsonElement parentesco)
            && parentesco.ValueKind == JsonValueKind.String
            && parentesco.GetString() == ProprioCandidato;
    }
}
