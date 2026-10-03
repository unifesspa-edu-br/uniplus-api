namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

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
    /// valem sempre. Sem isso, nenhuma resposta identificaria a ocorrência do candidato.
    /// </summary>
    public static List<FieldError> Conferir(
        int minimo,
        IReadOnlyList<(string FatoCodigo, bool TemExibicao, Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes)> subitens)
    {
        ArgumentNullException.ThrowIfNull(subitens);

        List<FieldError> erros = [];
        void Recusar(string campo, string mensagem) =>
            erros.Add(new(campo, new DomainError(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto, mensagem)));

        if (minimo < 1)
        {
            Recusar("minimo", "O grupo que inclui o candidato tem ao menos uma ocorrência: a do próprio candidato.");
        }

        int indice = subitens.ToList().FindIndex(static s => string.Equals(s.FatoCodigo, FatoParentesco, StringComparison.Ordinal));
        if (indice < 0)
        {
            Recusar("subitens", $"O grupo que inclui o candidato pede o parentesco ({FatoParentesco}) em cada ocorrência.");
            return erros;
        }

        (_, bool temExibicao, Obrigatoriedade obrigatoriedade, IReadOnlyList<RestricaoValor> restricoes) = subitens[indice];
        if (temExibicao || obrigatoriedade.Tipo != TipoObrigatoriedade.Sempre)
        {
            Recusar($"subitens[{indice}]", "O parentesco do grupo que inclui o candidato é sempre exibido e obrigatório.");
        }

        if (restricoes.OfType<OpcoesPermitidas>().Any(static o => !o.Entradas.Any(static e => e.Quando is null && e.Valores.Contains(ProprioCandidato))))
        {
            Recusar($"subitens[{indice}].restricoes",
                $"As opções de parentesco do grupo que inclui o candidato admitem sempre o próprio candidato ({ProprioCandidato}).");
        }

        return erros;
    }

    /// <summary>A lista respondida tem exatamente uma ocorrência do próprio candidato.</summary>
    public static bool TemUmaOcorrenciaDoCandidato(IEnumerable<OcorrenciaRespondida> ocorrencias)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);

        return ocorrencias.Count(static o =>
            o.Respostas.TryGetValue(FatoParentesco, out JsonElement parentesco)
            && parentesco.ValueKind == JsonValueKind.String
            && parentesco.GetString() == ProprioCandidato) == 1;
    }
}
