namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
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
            termo.Exibicao is { } exibicao ? Predicado(exibicao) : null,
            new ObrigatoriedadeDto(
                PredicadoDnfJson.ParaToken(termo.Obrigatoriedade.Tipo),
                termo.Obrigatoriedade.Predicado is { } predicado ? Predicado(predicado) : null));
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> Predicado(PredicadoDnf predicado) =>
        [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoPrecondicaoDto>)
            [.. c.Condicoes.Select(static d => new CondicaoPrecondicaoDto(d.Fato, d.Operador.ToCodigo(), d.Valor))])];
}
