namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A manifestação técnica de quem tem a permissão de se manifestar sobre a proteção de um item com aprovação solicitada
/// (UNI-REQ-0373). A nova manifestação da mesma pessoa no mesmo ciclo substitui a vigente sem apagá-la.
/// </summary>
/// <param name="Item">O cadastro e o identificador do item.</param>
/// <param name="Ciclo">O ciclo de aprovação lido no item; se o item foi reiniciado depois, a manifestação responde conflito.</param>
/// <param name="Conteudo">O parecer e o texto.</param>
/// <remarks>
/// As partes chegam como o corpo as informa, com campos anuláveis, para que o ausente seja recusado no próprio campo,
/// junto das demais violações, num único lote (ADR-0125): quem as lê é <see cref="PedidoDeManifestacao"/>.
/// </remarks>
public sealed record RegistrarManifestacaoDaProtecaoCommand(
    ItemInformado Item,
    int? Ciclo,
    ConteudoInformado Conteudo) : ICommand<Result>;
