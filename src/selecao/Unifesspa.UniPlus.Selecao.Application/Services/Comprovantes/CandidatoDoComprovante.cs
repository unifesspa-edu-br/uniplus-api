namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>
/// O titular do comprovante: o nome civil e o nome social vêm das respostas do envio, e o nome social só aparece quando o
/// candidato optou por ele. Os três fatos do nome ficam no cartão do titular e fora das seções, para não aparecerem duas vezes.
/// </summary>
internal static class CandidatoDoComprovante
{
    private static readonly string[] FatosDoNome =
        [ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.DesejaNomeSocial, ConjuntoBasicoDaInscricao.NomeSocial];

    public static CandidatoDoComprovanteDto De(InsumosDoComprovante insumos)
    {
        IReadOnlyDictionary<string, JsonElement> respostas = insumos.Comprovante.TodasAsRespostas();
        bool usaNomeSocial = respostas.TryGetValue(ConjuntoBasicoDaInscricao.DesejaNomeSocial, out JsonElement deseja)
            && deseja.ValueKind == JsonValueKind.True;

        return new CandidatoDoComprovanteDto(
            Texto(respostas, ConjuntoBasicoDaInscricao.Nome) ?? string.Empty,
            usaNomeSocial ? Texto(respostas, ConjuntoBasicoDaInscricao.NomeSocial) : null,
            usaNomeSocial,
            insumos.Inscricao.Cpf.Mascarado);
    }

    /// <summary>As respostas sem os fatos do nome, que o cartão do titular já mostra.</summary>
    public static Dictionary<string, JsonElement> SemOsFatosDoNome(IReadOnlyDictionary<string, JsonElement> respostas) =>
        respostas.Where(static r => !FatosDoNome.Contains(r.Key)).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    private static string? Texto(IReadOnlyDictionary<string, JsonElement> respostas, string codigo) =>
        respostas.TryGetValue(codigo, out JsonElement valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
