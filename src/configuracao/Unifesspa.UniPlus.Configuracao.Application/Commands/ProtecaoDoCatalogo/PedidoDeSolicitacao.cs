namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os itens que quem envia escolheu para a solicitação, cada um com a posição na lista, conferidos na forma antes de
/// qualquer leitura (ADR-0125). O item repetido conta uma vez.
/// </summary>
/// <param name="Itens">A posição de cada item na lista pedida e o item.</param>
internal sealed record PedidoDeSolicitacao(IReadOnlyList<(int Indice, ItemDaProtecao Item)> Itens)
{
    /// <summary>Os identificadores pedidos do cadastro, sem repetição.</summary>
    public HashSet<Guid> IdsDe(CadastroDoCatalogo cadastro) =>
        [.. Itens.Where(i => i.Item.Cadastro == cadastro).Select(static i => i.Item.ItemId)];

    /// <summary>A recusa de cada item pedido que não veio entre os pendentes lidos, na posição em que foi pedido.</summary>
    public IReadOnlyList<FieldError> NaoPendentes(IReadOnlySet<Guid> tiposLidos, IReadOnlySet<Guid> fatosLidos) =>
        [.. Itens
            .Where(i => !(i.Item.Cadastro == CadastroDoCatalogo.TipoDocumento ? tiposLidos : fatosLidos).Contains(i.Item.ItemId))
            .Select(static i => ItemNaoPendente(i.Indice))];

    /// <summary>
    /// O pedido conferido, ou o lote das recusas: o item nulo, o cadastro fora do vocabulário e o item sem
    /// identificador, cada um no campo da sua posição.
    /// </summary>
    public static Result<PedidoDeSolicitacao> Ler(IReadOnlyList<ItemParaSolicitarDto?> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);

        List<FieldError> erros = [];
        (int Indice, ItemDaProtecao? Item)[] lidos = [.. itens.Select((item, indice) => (indice, LerItem(item, indice, erros)))];

        return erros.Count > 0
            ? Result<PedidoDeSolicitacao>.ValidationFailure(erros)
            : Result<PedidoDeSolicitacao>.Success(new PedidoDeSolicitacao([.. lidos.Select(static l => (l.Indice, l.Item!.Value))]));
    }

    private static ItemDaProtecao? LerItem(ItemParaSolicitarDto? item, int indice, List<FieldError> erros)
    {
        FieldError? recusa = Recusa(item, indice);
        if (recusa is not null)
        {
            erros.Add(recusa);
            return null;
        }

        return new ItemDaProtecao(CadastrosDoCatalogo.Analisar(item!.Cadastro), item.Id);
    }

    private static FieldError? Recusa(ItemParaSolicitarDto? item, int indice) => item switch
    {
        null => new($"itens[{indice}]", new DomainError(
            ProtecaoDoCatalogoErrorCodes.CadastroInvalido, "O item deve informar o cadastro e o identificador.")),
        _ when !CadastrosDoCatalogo.TryAnalisar(item.Cadastro, out _) => new($"itens[{indice}].cadastro", new DomainError(
            ProtecaoDoCatalogoErrorCodes.CadastroInvalido,
            $"O cadastro do item deve ser {string.Join(" ou ", CadastrosDoCatalogo.TokensCanonicos)}.")),
        { Id: var id } when id == Guid.Empty => ItemNaoPendente(indice),
        _ => null,
    };

    private static FieldError ItemNaoPendente(int indice) => new($"itens[{indice}].id", new DomainError(
        ProtecaoDoCatalogoErrorCodes.ItemNaoPendente,
        "O item não existe ou não está pendente de solicitação; atualize a lista e selecione de novo."));
}
