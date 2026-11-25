namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Mapeamento da trilha da contenção para os registros que a API expõe, na ordem total da trilha.</summary>
public static class ContencaoMapping
{
    /// <summary>A decisão de uma etapa da retomada não é um evento da trilha: tem o tipo próprio.</summary>
    public const string TipoDaDecisao = "decisao_da_retomada";

    public static IReadOnlyList<RegistroDaContencaoDto> ToDto(TrilhaDaContencao trilha)
    {
        ArgumentNullException.ThrowIfNull(trilha);

        return
        [.. trilha.Eventos.Select(DoEvento)
            .Concat(trilha.Decisoes.Select(DaDecisao))
            .OrderBy(static r => r.Ordem)];
    }

    private static RegistroDaContencaoDto DoEvento(EventoDaContencao evento) =>
        new(
            evento.Id,
            evento.Tipo.Codigo(),
            (evento.SuspensaoId ?? evento.DesativacaoId)!.Value,
            evento.OrdemNoHistorico,
            evento.Instante,
            evento.Ator.Codigo(),
            evento.AtorEmissor is null || evento.AtorContaId is null ? null : new IdentidadeDto(evento.AtorEmissor, evento.AtorContaId, evento.AtorNome),
            evento.Papel?.Codigo(),
            evento.Motivo,
            evento.Origem?.Codigo(),
            evento.PerfilProtegido,
            null);

    private static RegistroDaContencaoDto DaDecisao(DecisaoDaRetomada decisao) =>
        new(
            decisao.Id,
            TipoDaDecisao,
            decisao.SuspensaoId,
            decisao.OrdemNoHistorico,
            decisao.Instante,
            AtorDoEventoDaContencao.Humano.Codigo(),
            new IdentidadeDto(decisao.DecisorEmissor, decisao.DecisorContaId, decisao.DecisorNome),
            decisao.Etapa.Codigo(),
            decisao.Motivo,
            null,
            decisao.PerfilProtegidoNaDecisao,
            decisao.DecisorTemConcessao);
}
