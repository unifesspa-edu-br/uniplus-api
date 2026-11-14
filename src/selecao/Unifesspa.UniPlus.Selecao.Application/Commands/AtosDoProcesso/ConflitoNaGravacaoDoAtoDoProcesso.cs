namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Errors;

using Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A violação de unicidade que só a gravação enxerga e que o registro do ato do processo deixa passar: a correção de um
/// ato que outra requisição já corrigiu e o item do edital que outra requisição já publicou. As violações do registro
/// central de atos não são da Seleção, e quem as traduz é a porta de escrita.
/// </summary>
internal static class ConflitoNaGravacaoDoAtoDoProcesso
{
    private const string ConstraintDaCorrecaoUnica = "ux_atos_do_processo_ato_corrigido";
    private const string ConstraintDaPublicacaoOriginalUnica = "ux_atos_do_processo_item_original";

    /// <returns>A recusa nomeada, ou <see langword="null"/> quando a constraint não é do registro do ato do processo.</returns>
    public static DomainError? Traduzir(string? constraint, AtoDoProcesso ato)
    {
        ArgumentNullException.ThrowIfNull(ato);

        return constraint switch
        {
            ConstraintDaCorrecaoUnica when ato.AtoCorrigidoId is { } corrigido => new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido,
                ConferenciaDoAtoCorrigido.MensagemDoAtoJaCorrigido(corrigido, corretor: null)),
            ConstraintDaPublicacaoOriginalUnica when ato.ItemDoEdital is not null => new DomainError(
                AtoDoProcessoErrorCodes.ProdutoJaPublicado,
                ProjecaoDosProdutosPrevistos.MensagemDoItemJaPublicado),
            _ => null,
        };
    }
}
