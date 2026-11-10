namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura da trilha da contenção: confere a permissão, registra o desfecho e devolve os registros em ordem.</summary>
public static class ObterContencaoDoPerfilQueryHandler
{
    public static async Task<Result<IReadOnlyList<RegistroDaContencaoDto>>> Handle(
        ObterContencaoDoPerfilQuery query,
        IVerificadorDeAcesso verificador,
        IPerfilRepository perfis,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(perfis);

        DomainError? negado = await NegadoAsync(verificador, query.PerfilId, cancellationToken).ConfigureAwait(false);
        TrilhaDaContencao? trilha = negado is null
            ? await perfis.ObterTrilhaDaContencaoAsync(query.PerfilId, cancellationToken).ConfigureAwait(false)
            : null;

        return negado is not null ? Recusa(negado)
            : trilha is null ? Recusa(PerfilHandlerErros.NaoEncontrado(query.PerfilId))
            : Result<IReadOnlyList<RegistroDaContencaoDto>>.Success(ContencaoMapping.ToDto(trilha));
    }

    private static Result<IReadOnlyList<RegistroDaContencaoDto>> Recusa(DomainError erro) =>
        Result<IReadOnlyList<RegistroDaContencaoDto>>.Failure(erro);

    private static async Task<DomainError?> NegadoAsync(IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        AcessoDaContencao? acesso = await AcessoDaContencao.ConsultarAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return PerfilHandlerErros.AutorNaoIdentificado();
        }

        await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);

        return acesso.Habilitado ? null : PerfilErros.SemPermissaoNaEtapa();
    }
}
