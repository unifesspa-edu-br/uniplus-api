namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
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
            CodigoComRotulo<TipoMotivoDeDecisao>.De(motivo.Tipo),
            motivo.Codigo.Valor,
            motivo.Descricao,
            motivo.Subclassificacao,
            CodigoComRotulo<SentidoDoMotivo>.De(motivo.Sentido),
            motivo.Ativo);
    }

    public static MotivoDeDecisaoDoProcessoDto ToDto(MotivoDeDecisaoDoProcesso motivo)
    {
        ArgumentNullException.ThrowIfNull(motivo);

        return new MotivoDeDecisaoDoProcessoDto(
            motivo.MotivoDeDecisaoOrigemId,
            new LugarDoMotivoDto(motivo.Lugar.FaseCanonicaOrigemId, motivo.Lugar.EtapaProcessoId),
            CodigoComRotulo<TipoMotivoDeDecisao>.De(motivo.Tipo),
            motivo.Codigo.Valor,
            motivo.Descricao,
            motivo.Subclassificacao,
            CodigoComRotulo<SentidoDoMotivo>.De(motivo.Sentido),
            new VigenciaDoMotivoDto(motivo.Vigencia.VersaoDoEditalDeEntrada, motivo.Vigencia.VersaoDoEditalDeSaida),
            motivo.JustificativaDaInclusao);
    }
}
