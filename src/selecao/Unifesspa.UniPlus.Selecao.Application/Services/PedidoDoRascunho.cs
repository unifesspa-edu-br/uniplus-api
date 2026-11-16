namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que o candidato pediu para salvar na própria inscrição em rascunho, já ligado à versão do formulário em que ela está
/// e ao instante que conferiu o período, com o corpo lido.
/// </summary>
public sealed record PedidoDoRascunho(Inscricao Inscricao, ContextoDaVersaoFixada Contexto, EnvioDasRespostas Envio, CorpoDoRascunho Corpo);

/// <summary>A inscrição editável do candidato e o instante em que o período dela foi conferido.</summary>
public sealed record InscricaoNoPeriodo(Inscricao Inscricao, DateTimeOffset Instante);

/// <summary>O corpo da requisição já lido: respostas, ocorrências de grupo e aceites de termo.</summary>
public sealed record CorpoDoRascunho(
    Dictionary<string, JsonElement> Respostas,
    Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos,
    List<AceiteDeTermo> Aceites)
{
    public static Result<CorpoDoRascunho> De(InscricaoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> grupos = InscricaoRequestMapper.ParaGrupos(request.Grupos);
        Result<List<AceiteDeTermo>> aceites = InscricaoRequestMapper.ParaAceites(request.Aceites);
        if (grupos.IsFailure || aceites.IsFailure)
        {
            return Result<CorpoDoRascunho>.ValidationFailure(grupos.IsFailure ? grupos.Errors : aceites.Errors);
        }

        return Result<CorpoDoRascunho>.Success(new CorpoDoRascunho(InscricaoRequestMapper.ParaRespostas(request.Respostas), grupos.Value!, aceites.Value!));
    }
}
