namespace Unifesspa.UniPlus.Selecao.Application.Queries.Rascunhos;

using System.Text.Json.Nodes;

using DTOs;

using Kernel.Results;

using Services;
using Services.Rascunhos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="ObterRascunhoDeProcessoSeletivoQuery"/>. O filtro da rota decide o acesso pela unidade que a linha
/// tinha; o handler o confirma pela unidade que ela tem ao ser lida, antes de exportar o documento.
/// </summary>
public static class ObterRascunhoDeProcessoSeletivoQueryHandler
{
    public static async Task<Result<RascunhoExportadoDto>> Handle(
        ObterRascunhoDeProcessoSeletivoQuery query,
        IRascunhoDeProcessoSeletivoRepository rascunhos,
        IVerificadorDeAcesso verificador,
        ExportacaoDoRascunho exportacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(rascunhos);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(exportacao);

        Result<RascunhoDeProcessoSeletivo> lido = await LerAcessivelAsync(query.Id, rascunhos, verificador, cancellationToken).ConfigureAwait(false);
        if (lido.IsFailure)
        {
            return Result<RascunhoExportadoDto>.Failure(lido.Error!);
        }

        JsonObject documento = await exportacao.ExportarAsync(lido.Value!, cancellationToken).ConfigureAwait(false);
        return Result<RascunhoExportadoDto>.Success(new RascunhoExportadoDto(documento, lido.Value!.ETag));
    }

    private static async Task<Result<RascunhoDeProcessoSeletivo>> LerAcessivelAsync(
        Guid id, IRascunhoDeProcessoSeletivoRepository rascunhos, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        RascunhoDeProcessoSeletivo? rascunho = await rascunhos.ObterAsync(id, cancellationToken).ConfigureAwait(false);
        if (rascunho is null)
        {
            return Result<RascunhoDeProcessoSeletivo>.Failure(AcessoAoProcesso.NaoEncontrado(id));
        }

        Result acesso = await AcessoAoProcesso
            .ExigirConfigurarNaUnidadeAtualAsync(verificador, rascunho.UnidadeAdministradoraOrigemId, id, cancellationToken)
            .ConfigureAwait(false);

        return acesso.IsFailure
            ? Result<RascunhoDeProcessoSeletivo>.Failure(acesso.Error!)
            : Result<RascunhoDeProcessoSeletivo>.Success(rascunho);
    }
}
