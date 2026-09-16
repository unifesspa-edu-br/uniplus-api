namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Handler de <see cref="DescartarRascunhoDaPublicacaoCommand"/>.
/// </summary>
/// <remarks>
/// Descartar o que não existe é sucesso, não erro: o operador pediu que não houvesse rascunho,
/// e não há. Devolver "não encontrado" obrigaria a tela a tratar como falha um estado que é
/// exatamente o desejado.
/// </remarks>
public static class DescartarRascunhoDaPublicacaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRascunhoDaPublicacaoCommand command,
        IRascunhoDePublicacaoRepository rascunhoRepository,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(rascunhoRepository);
        ArgumentNullException.ThrowIfNull(userContext);

        if (SalvarRascunhoDaPublicacaoCommandHandler.DonoDoRascunho(userContext) is not { } usuarioSub)
        {
            return Result.Failure(RascunhoDaPublicacao.SemDono);
        }

        // Apagar o que não existe afeta zero linhas e continua sendo sucesso — o pedido era
        // que não houvesse rascunho.
        await rascunhoRepository
            .ApagarDoOperadorAsync(command.ProcessoSeletivoId, usuarioSub, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
