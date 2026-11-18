namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public static class FaseCanonicaMapping
{
    public static FaseCanonicaDto ToDto(this FaseCanonica fase)
    {
        ArgumentNullException.ThrowIfNull(fase);
        return new FaseCanonicaDto(
            fase.Id, fase.Codigo.Valor, fase.Nome, fase.Descricao,
            fase.Sinalizadores.AgrupaEtapas, fase.Sinalizadores.PermiteComplementacao,
            FaseCanonicaCatalogo.AdmiteAgruparEtapas(fase.Codigo.Valor), FaseCanonicaCatalogo.AdmiteComplementacao(fase.Codigo.Valor),
            fase.BaseLegal, fase.Sinalizadores.ColetaInscricao, fase.Sinalizadores.ColetaSolicitacaoIsencao,
            OrigensDataFase.ParaTokenCanonico(fase.OrigemData),
            ParaDto(fase.UnidadeSugerida), fase.CreatedAt);
    }

    private static UnidadeSugeridaDto? ParaDto(UnidadeSugerida? unidade) =>
        unidade is null ? null : new UnidadeSugeridaDto(unidade.OrigemId, unidade.Sigla, unidade.Nome);
}
