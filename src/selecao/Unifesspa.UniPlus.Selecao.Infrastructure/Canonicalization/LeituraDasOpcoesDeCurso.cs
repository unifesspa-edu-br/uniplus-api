namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Leitura do bloco do número de opções de curso.</summary>
internal static class LeituraDasOpcoesDeCurso
{
    /// <summary>
    /// Lê o bloco do número de opções de curso. O intervalo é o mesmo que o agregado exige ao
    /// declará-lo: um envelope adulterado com outro valor é malformado, e não reidratado como se o
    /// domínio o aceitasse.
    /// </summary>
    internal static int? Ler(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "opcoesDeCurso", "$");
        leitor.ExigirChaves(bloco, "opcoesDeCurso", "numero");
        int numero = leitor.Inteiro(bloco, "numero", "opcoesDeCurso");

        return leitor.Falhou ? null : DentroDoIntervalo(leitor, numero);
    }

    private static int? DentroDoIntervalo(LeitorEnvelope leitor, int numero) =>
        numero is >= 1 and <= ProcessoSeletivo.MaximoDeOpcoesDeCurso
            ? numero
            : leitor.Propagar<int?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'opcoesDeCurso.numero' congelado deve ser 1 ou {ProcessoSeletivo.MaximoDeOpcoesDeCurso}."));
}
