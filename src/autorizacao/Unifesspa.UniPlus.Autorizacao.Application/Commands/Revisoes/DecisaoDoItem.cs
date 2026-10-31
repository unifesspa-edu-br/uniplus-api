namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que confirmar e revogar um item têm em comum antes de decidir: quem decide, se tem a permissão de quem decide aquele
/// item, a revisão e a concessão. Sem nenhuma permissão da revisão a recusa é 403; com a da outra governança, é de item.
/// A identidade incompleta responde antes de tudo.
/// </summary>
internal sealed record DecisaoDoItem(RevisaoDeAcessos Revisao, ItemDaRevisao Item, Concessao Concessao, Identidade Decisor)
{
    public static async Task<Result<DecisaoDoItem>> PrepararAsync(
        Guid revisaoId,
        Guid itemId,
        IRevisaoDeAcessosRepository revisoes,
        IConcessaoRepository concessoes,
        IVerificadorDeAcesso verificador,
        Identidade? decisor,
        CancellationToken cancellationToken)
    {
        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        if (decisor is null)
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.AutorNaoIdentificado());
        }

        if (!acesso.Le)
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.SemPermissao());
        }

        RevisaoDeAcessos? revisao = await revisoes.ObterParaAlterarAsync(revisaoId, cancellationToken).ConfigureAwait(false);
        if (revisao is null)
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.NaoEncontrada());
        }

        ItemDaRevisao? item = revisao.Itens.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.ItemNaoEncontrado());
        }

        if (!acesso.Decide(item.Decide))
        {
            return Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.DecisorNaoDecideOItem());
        }

        Concessao? concessao = await concessoes.ObterParaAlterarAsync(item.ConcessaoId, cancellationToken).ConfigureAwait(false);

        return concessao is null
            ? Result<DecisaoDoItem>.Failure(RevisaoHandlerErros.ItemNaoEncontrado())
            : Result<DecisaoDoItem>.Success(new DecisaoDoItem(revisao, item, concessao, decisor));
    }
}
