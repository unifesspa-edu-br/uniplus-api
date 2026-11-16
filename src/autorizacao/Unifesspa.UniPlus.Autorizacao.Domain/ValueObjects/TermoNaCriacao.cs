namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O termo como a criação do pedido o encontra: a versão vigente, lida sob a trava, e a que o cliente apresentou.</summary>
/// <param name="Vigente">A versão vigente; nula se nenhuma foi publicada ou se o pedido não exige termo e ela não foi lida.</param>
/// <param name="Apresentada">A versão que o cliente diz ter apresentado; nula se não apresentou nenhuma.</param>
/// <param name="Exigido">O perfil do pedido exige o termo (<see cref="ExigenciaDoTermo"/>).</param>
public sealed record TermoNaCriacao(VersaoDoTermo? Vigente, Guid? Apresentada, bool Exigido)
{
    internal Result? Recusa() =>
        Exigido && Vigente is null
            ? Result.Failure(new DomainError(
                PedidoErrorCodes.SemTermoVigente,
                "O perfil pedido dá acesso a dado pessoal ou sensível e exige o aceite do termo de responsabilidade, mas nenhuma versão foi publicada. Peça ao gestor de acessos que publique o termo."))
            : ApresentacaoDoTermo.Conferir(Apresentada, Exigido ? Vigente!.Id : null);
}
