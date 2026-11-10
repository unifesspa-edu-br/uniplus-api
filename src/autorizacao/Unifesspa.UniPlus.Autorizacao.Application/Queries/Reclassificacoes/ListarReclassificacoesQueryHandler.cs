namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A fila de reclassificações da etapa pedida.</summary>
public static class ListarReclassificacoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>>> Handle(
        ListarReclassificacoesQuery query, IVerificadorDeAcesso verificador, FilaDeReclassificacoes fila, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(fila);

        if (EtapaDaVersaoCodigo.TentarLer(query.Etapa) is not { } etapa)
        {
            return Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>>.Failure(ReclassificacaoErros.EtapaInvalida());
        }

        bool? pode = await AcessoDaVersao.PodeListarAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return pode is true
            ? Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>>.Success(await fila.MontarAsync(etapa, cancellationToken).ConfigureAwait(false))
            : Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>>.Failure(
                pode is null ? PerfilHandlerErros.AutorNaoIdentificado() : ReclassificacaoErros.SemPermissaoNaEtapa());
    }
}
