namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Uma oferta que o candidato escolheu, nomeada como a versão do formulário a que a inscrição está presa a nomeia.
/// </summary>
/// <param name="Id">A oferta escolhida.</param>
/// <param name="Nome">O nome da oferta, como o edital a nomeou; nulo quando a versão não a traz, para a escolha nunca ser escondida.</param>
/// <param name="Ordem">A ordem da escolha, a partir de 1 e sem lacuna.</param>
public sealed record OfertaEscolhidaDto(Guid Id, string? Nome, int Ordem);
