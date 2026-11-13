namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A manifestação pedida, conferida na forma antes de qualquer leitura do item (ADR-0125): o item, o ciclo lido e o
/// conteúdo. Todas as recusas de forma saem num único lote.
/// </summary>
/// <param name="Item">O cadastro e o identificador do item.</param>
/// <param name="Ciclo">O ciclo de aprovação que quem se manifesta leu no item.</param>
/// <param name="Conteudo">O parecer e o texto.</param>
public sealed record PedidoDeManifestacao(ItemDaProtecao Item, CicloDaAprovacao Ciclo, ConteudoDaManifestacao Conteudo)
{
    /// <summary>
    /// O pedido conferido, ou o lote das recusas: cadastro fora do vocabulário, ciclo ausente ou menor que 1, parecer
    /// fora do vocabulário, texto ausente no parecer desfavorável e texto longo demais.
    /// </summary>
    public static Result<PedidoDeManifestacao> Ler(RegistrarManifestacaoDaProtecaoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<FieldError> erros = [];
        ItemDaProtecao? item = ItemDaProtecao.Ler(command.Cadastro, command.ItemId, erros);
        CicloDaAprovacao? ciclo = CicloDaAprovacao.Ler(command.Ciclo, erros);
        ConteudoDaManifestacao? conteudo = ConteudoDaManifestacao.Ler(command.Parecer, command.Texto, erros);

        return erros.Count > 0
            ? Result<PedidoDeManifestacao>.ValidationFailure(erros)
            : Result<PedidoDeManifestacao>.Success(new PedidoDeManifestacao(item!.Value, ciclo!.Value, conteudo!));
    }
}
