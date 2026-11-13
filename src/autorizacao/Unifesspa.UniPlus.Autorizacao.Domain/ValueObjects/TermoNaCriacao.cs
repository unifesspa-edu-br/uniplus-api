namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>O termo como a criação do pedido o encontra: a versão vigente, lida sob a trava, e a que o cliente apresentou.</summary>
/// <param name="Vigente">A versão vigente; nula se nenhuma foi publicada ou se o pedido não exige termo e ela não foi lida.</param>
/// <param name="Apresentada">A versão que o cliente diz ter apresentado; nula se não apresentou nenhuma.</param>
/// <param name="Exigido">O perfil do pedido exige o termo (<see cref="PedidoDeConcessao.ExigeTermo"/>).</param>
public sealed record TermoNaCriacao(VersaoDoTermo? Vigente, Guid? Apresentada, bool Exigido);

/// <summary>A versão do termo fixada no pedido, com o número e o resumo copiados dela, imutável depois da criação.</summary>
public sealed record TermoFixado(Guid VersaoId, int Numero, string Resumo);
