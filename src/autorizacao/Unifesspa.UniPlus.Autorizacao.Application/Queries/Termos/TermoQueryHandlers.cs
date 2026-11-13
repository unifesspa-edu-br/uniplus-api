namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Termos;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da versão vigente: sem trava, porque a leitura não fixa nada; quem fixa é a criação do pedido.</summary>
public static class ObterTermoVigenteQueryHandler
{
    public static async Task<Result<VersaoDoTermoDto>> Handle(
        ObterTermoVigenteQuery query, ITermoDeResponsabilidadeRepository termos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termos);

        VersaoDoTermo? vigente = await termos.ObterVigenteAsync(cancellationToken).ConfigureAwait(false);

        return vigente is null
            ? Result<VersaoDoTermoDto>.Failure(TermoHandlerErros.SemVersaoVigente())
            : Result<VersaoDoTermoDto>.Success(TermoMapping.ToDto(vigente, vigente.Id));
    }
}

/// <summary>Handler da versão pelo id, vigente ou não: o pedido guarda a versão que fixou.</summary>
public static class ObterVersaoDoTermoQueryHandler
{
    public static async Task<Result<VersaoDoTermoDto>> Handle(
        ObterVersaoDoTermoQuery query, ITermoDeResponsabilidadeRepository termos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(termos);

        VersaoDoTermo? versao = await termos.ObterVersaoAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (versao is null)
        {
            return Result<VersaoDoTermoDto>.Failure(TermoHandlerErros.VersaoNaoEncontrada());
        }

        VersaoDoTermo? vigente = await termos.ObterVigenteAsync(cancellationToken).ConfigureAwait(false);
        return Result<VersaoDoTermoDto>.Success(TermoMapping.ToDto(versao, vigente?.Id));
    }
}

/// <summary>Handler do histórico das versões, sem o texto.</summary>
public static class ListarVersoesDoTermoQueryHandler
{
    public static async Task<Result<ListarVersoesDoTermoResult>> Handle(
        ListarVersoesDoTermoQuery query, ITermoDeResponsabilidadeRepository termos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(termos);

        (IReadOnlyList<VersaoDoTermo> itens, (string, Guid)? anterior, (string, Guid)? proximo) = await termos
            .ListarVersoesAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken).ConfigureAwait(false);
        Guid? vigenteId = (await termos.ObterVigenteAsync(cancellationToken).ConfigureAwait(false))?.Id;

        return Result<ListarVersoesDoTermoResult>.Success(
            new ListarVersoesDoTermoResult([.. itens.Select(v => TermoMapping.ToResumo(v, vigenteId))], anterior, proximo));
    }
}
