namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// A versão do catálogo escolhida para o termo: o termo e a versão, o código e o número delas e o conteúdo
/// congelado. A escrita lê só a identidade, o mesmo que <see cref="Unifesspa.UniPlus.Regras.Entradas.VersaoDoTermoInput"/> traz.
/// </summary>
public sealed record VersaoDoTermoExigidoDto(Guid TermoId, Guid VersaoId, string TermoCodigo, int VersaoNumero, ConteudoDoTermoExigidoDto Conteudo);

/// <summary>Conteúdo da versão do termo exigido pelo formulário.</summary>
public sealed record ConteudoDoTermoExigidoDto(
    string Nome, string Texto, string BaseLegal, CodigoComRotulo<FormaAceite> FormaAceite, string HashVersao);

/// <summary>
/// Um termo exigido pelo formulário (UNI-REQ-0086): a exigência, a versão escolhida no catálogo com o conteúdo
/// congelado, e as condições de exibição (nula quando sempre aparece) e de obrigatoriedade.
/// </summary>
public sealed record TermoExigidoDto(string Codigo, int Ordem, VersaoDoTermoExigidoDto Termo, ExibicaoEObrigatoriedadeDto Regras);
