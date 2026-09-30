namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal static class TermoExigidoMapping
{
    public static TermoExigidoDto ToDto(this TermoExigidoFormulario termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        return new TermoExigidoDto(
            termo.Codigo,
            termo.Ordem,
            termo.TermoId,
            termo.VersaoId,
            termo.Nome,
            termo.Texto,
            termo.BaseLegal,
            termo.FormaAceite,
            termo.HashVersao,
            termo.Exibicao?.ToDto(),
            termo.Obrigatoriedade.ToDto());
    }
}
