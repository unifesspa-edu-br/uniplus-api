namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma aresta do grafo de dependência conjunto (Story #928, §6): aponta do produtor
/// (<see cref="Origem"/>) para o consumidor (<see cref="Destino"/>). O mesmo par de nós pode ter mais
/// de uma aresta se de <see cref="TipoArestaGrafo"/> distinto — por isso o tipo entra na identidade.
/// </summary>
public sealed record ArestaGrafoDependencia(
    TipoArestaGrafo Tipo,
    NoGrafoDependencia Origem,
    NoGrafoDependencia Destino);
