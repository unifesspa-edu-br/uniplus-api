namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Mapeamento dos motivos do cadastro e dos escolhidos pelo processo para os DTOs.</summary>
public static class MotivoDeDecisaoMapping
{
    public static MotivoDeDecisaoDto ToDto(MotivoDeDecisao motivo)
    {
        ArgumentNullException.ThrowIfNull(motivo);

        return new MotivoDeDecisaoDto(
            motivo.Id,
            motivo.Tipo.ToCodigo(),
            motivo.Codigo.Valor,
            motivo.Descricao,
            motivo.Subclassificacao,
            motivo.Sentido.ToCodigo(),
            motivo.Ativo);
    }

    public static MotivoDoProcessoDto ToDto(MotivoDoProcesso motivo)
    {
        ArgumentNullException.ThrowIfNull(motivo);

        return new MotivoDoProcessoDto(
            motivo.MotivoDeDecisaoOrigemId,
            motivo.Tipo.ToCodigo(),
            motivo.Codigo.Valor,
            motivo.Descricao,
            motivo.Subclassificacao,
            motivo.Sentido.ToCodigo(),
            motivo.Congelado,
            motivo.JustificativaDaInclusao);
    }
}
