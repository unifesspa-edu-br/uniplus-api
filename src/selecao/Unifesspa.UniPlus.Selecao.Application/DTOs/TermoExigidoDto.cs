namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// A versão do catálogo escolhida para o termo: o termo e a versão, o código e o número delas e o conteúdo
/// congelado. A escrita lê só a identidade, o mesmo que <see cref="Unifesspa.UniPlus.Regras.Entradas.VersaoDoTermoInput"/> traz.
/// </summary>
public sealed record VersaoDoTermoDto(Guid TermoId, Guid VersaoId, string TermoCodigo, int VersaoNumero, ConteudoDoTermoDto Conteudo);

/// <summary>
/// Um termo exigido pelo formulário (UNI-REQ-0086): a exigência, a versão escolhida no catálogo com o conteúdo
/// congelado, e as condições de exibição (nula quando sempre aparece) e de obrigatoriedade.
/// </summary>
public sealed record TermoExigidoDto(string Codigo, int Ordem, VersaoDoTermoDto Termo, ExibicaoEObrigatoriedadeDto Regras);
