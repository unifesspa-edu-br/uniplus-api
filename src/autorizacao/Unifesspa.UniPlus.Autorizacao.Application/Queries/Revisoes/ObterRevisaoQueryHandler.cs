namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da leitura de uma revisão. Lê quem conduz a revisão, quem revê os acessos dos Representantes do controlador ou
/// quem consulta as revisões; os itens da concessão de quem lê vêm marcados.
/// </summary>
public static class ObterRevisaoQueryHandler
{
    public static async Task<Result<RevisaoDeAcessosDto>> Handle(
        ObterRevisaoQuery query,
        LeituraDaRevisao leitura,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, query.Id, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        await acesso.RegistrarLeituraAsync(verificador, cancellationToken).ConfigureAwait(false);
        return acesso.Le
            ? await LerAsync(leitura, query.Id, timeProvider.GetUtcNow(), IdentidadeDoToken.De(userContext), cancellationToken).ConfigureAwait(false)
            : Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.SemPermissao());
    }

    private static async Task<Result<RevisaoDeAcessosDto>> LerAsync(
        LeituraDaRevisao leitura, Guid revisaoId, DateTimeOffset agora, Identidade? leitor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(leitura);

        return await leitura.LerAsync(revisaoId, agora, leitor, cancellationToken).ConfigureAwait(false) is { } revisao
            ? Result<RevisaoDeAcessosDto>.Success(revisao)
            : Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.NaoEncontrada());
    }
}
