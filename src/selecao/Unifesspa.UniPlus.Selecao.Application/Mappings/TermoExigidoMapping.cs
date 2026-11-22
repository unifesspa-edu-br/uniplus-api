namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal static class TermoExigidoMapping
{
    public static TermoExigidoDto ToDto(this TermoExigidoFormulario termo) =>
        new(
            termo.Codigo,
            termo.Ordem,
            new VersaoDoTermoDto(
                termo.TermoId,
                termo.VersaoId,
                termo.TermoCodigo,
                termo.VersaoNumero,
                new ConteudoDoTermoExigidoDto(termo.Nome, termo.Texto, termo.BaseLegal, CodigoComRotulo<FormaAceite>.De(termo.FormaAceite), termo.HashVersao)),
            new ExibicaoEObrigatoriedadeDto(termo.Exibicao?.ToDto(), termo.Obrigatoriedade.ToDto()));
}
