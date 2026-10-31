namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura de uma revisão. Lê quem conduz a revisão ou quem revê os acessos da proteção de dados.</summary>
public static class ObterRevisaoQueryHandler
{
    public static async Task<Result<RevisaoDeAcessosDto>> Handle(
        ObterRevisaoQuery query,
        IRevisaoDeAcessosRepository repository,
        IVerificadorDeAcesso verificador,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        if (!acesso.Le)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.SemPermissao());
        }

        RevisaoDetalhada? detalhada = await repository
            .ObterDetalhadaAsync(query.Id, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return detalhada is null
            ? Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.NaoEncontrada())
            : Result<RevisaoDeAcessosDto>.Success(RevisaoMapping.ToDto(detalhada));
    }
}
