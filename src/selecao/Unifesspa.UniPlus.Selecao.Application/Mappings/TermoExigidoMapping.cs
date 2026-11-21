namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

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
                new ConteudoDoTermoDto(termo.Nome, termo.Texto, termo.BaseLegal, termo.FormaAceite, termo.HashVersao)),
            new ExibicaoEObrigatoriedadeDto(termo.Exibicao?.ToDto(), termo.Obrigatoriedade.ToDto()));
}
