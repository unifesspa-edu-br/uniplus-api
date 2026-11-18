namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal static class TermoExigidoMapping
{
    public static TermoExigidoDto ToDto(this TermoExigidoFormulario termo) =>
        new(
            termo.Codigo,
            termo.Ordem,
            termo.TermoId,
            termo.VersaoId,
            termo.TermoCodigo,
            termo.VersaoNumero,
            termo.Nome,
            termo.Texto,
            termo.BaseLegal,
            termo.FormaAceite,
            termo.HashVersao,
            termo.Exibicao?.ToDto(),
            termo.Obrigatoriedade.ToDto());
}
