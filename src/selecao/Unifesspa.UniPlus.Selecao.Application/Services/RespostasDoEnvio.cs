namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As respostas em que o envio se confere: o estado que SERÁ persistido (o gravado mesclado com o corpo recebido, nunca só o
/// delta desta chamada), as recusas da forma e do endereço que não dependem do Geo e os campos recusados, que não entram nos
/// derivados nem na conferência do Geo.
/// </summary>
public sealed record RespostasDoEnvio(
    IReadOnlyDictionary<string, JsonElement> Mescladas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GruposMesclados,
    List<FieldError> Recusas,
    HashSet<string> CamposRecusados)
{
    public Dictionary<string, JsonElement> Conferiveis =>
        Mescladas.Where(r => !CamposRecusados.Contains(r.Key)).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    public bool TemGrupoComIdRepetido => Recusas.Any(static r => r.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado);
}
