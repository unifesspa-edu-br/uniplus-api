namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento <c>MotivoDecisaoIsencao</c> → <c>MotivoDecisaoIsencaoDto</c>.
/// </summary>
public static class MotivoDecisaoIsencaoMapping
{
    public static MotivoDecisaoIsencaoDto ToDto(MotivoDecisaoIsencao motivo)
    {
        ArgumentNullException.ThrowIfNull(motivo);

        return new MotivoDecisaoIsencaoDto(
            Id: motivo.Id,
            Codigo: motivo.Codigo.Valor,
            Descricao: motivo.Descricao,
            Fundamento: motivo.Fundamento.ToCodigo(),
            ResultadoPermitido: motivo.ResultadoPermitido.ToCodigo(),
            Ativo: motivo.Ativo);
    }
}
