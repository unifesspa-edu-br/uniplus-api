namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da fila de aprovação de uma etapa: lê a etapa pedida, recusa quem não tem a permissão dela e pede a fila.</summary>
public static class ListarFilaDeVersoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>> Handle(
        ListarFilaDeVersoesQuery query,
        IVerificadorDeAcesso verificador,
        FilaDeVersoesDaEtapa fila,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(fila);

        if (EtapaDaVersaoCodigo.TentarLer(query.Fila) is not { } etapa)
        {
            return Recusa(PerfilErros.EtapaInvalida());
        }

        DomainError? negado = await NegadoAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return negado is not null
            ? Recusa(negado)
            : Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>.Success(await fila.MontarAsync(etapa, cancellationToken).ConfigureAwait(false));
    }

    private static Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> Recusa(DomainError erro) =>
        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>.Failure(erro);

    private static async Task<DomainError?> NegadoAsync(IVerificadorDeAcesso verificador, EtapaDaVersao etapa, CancellationToken cancellationToken)
    {
        bool? pode = await AcessoDaVersao.PodeListarAsync(verificador, etapa, cancellationToken).ConfigureAwait(false);

        return pode switch
        {
            null => PerfilHandlerErros.AutorNaoIdentificado(),
            false => PerfilErros.SemPermissaoNaEtapa(),
            _ => null,
        };
    }
}
