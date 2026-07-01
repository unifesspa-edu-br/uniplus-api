namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class FaseCanonicaMapping
{
    public static FaseCanonicaDto ToDto(this FaseCanonica fase)
    {
        ArgumentNullException.ThrowIfNull(fase);
        return new FaseCanonicaDto(
            fase.Id,
            fase.Codigo.Valor,
            fase.Nome,
            fase.Descricao,
            DonosTipicos.ParaTokenCanonico(fase.DonoTipico),
            fase.AgrupaEtapas,
            fase.PermiteComplementacao,
            fase.BaseLegal,
            fase.CreatedAt);
    }
}
