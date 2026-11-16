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

/// <summary>O que o candidato enviou: as respostas, as ocorrências dos grupos e os aceites de termo, já lidos do pedido.</summary>
public sealed record CorpoDoEnvio(
    Dictionary<string, JsonElement> Respostas,
    Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos,
    List<AceiteDeTermo> Aceites)
{
    public static Result<CorpoDoEnvio> De(InscricaoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> grupos = InscricaoRequestMapper.ParaGrupos(request.Grupos);
        Result<List<AceiteDeTermo>> aceites = InscricaoRequestMapper.ParaAceites(request.Aceites);
        if (grupos.IsFailure || aceites.IsFailure)
        {
            return Result<CorpoDoEnvio>.ValidationFailure(grupos.IsFailure ? grupos.Errors : aceites.Errors);
        }

        return Result<CorpoDoEnvio>.Success(new CorpoDoEnvio(InscricaoRequestMapper.ParaRespostas(request.Respostas), grupos.Value!, aceites.Value!));
    }
}
