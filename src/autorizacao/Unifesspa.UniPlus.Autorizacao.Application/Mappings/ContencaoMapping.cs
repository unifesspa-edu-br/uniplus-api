namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

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
            evento.Tipo.ToCodigo(),
            (evento.SuspensaoId ?? evento.DesativacaoId)!.Value,
            evento.OrdemNaTrilha,
            evento.Instante,
            evento.Ator.ToCodigo(),
            evento.AtorNome,
            evento.Papel?.ToCodigo(),
            evento.Motivo,
            evento.Origem?.ToCodigo(),
            evento.PerfilProtegido,
            null);

    private static RegistroDaContencaoDto DaDecisao(DecisaoDaRetomada decisao) =>
        new(
            decisao.Id,
            TipoDaDecisao,
            decisao.SuspensaoId,
            decisao.OrdemNaTrilha,
            decisao.Instante,
            AtorDoEventoDaContencao.Humano.ToCodigo(),
            decisao.DecisorNome,
            decisao.Etapa.ToCodigo(),
            decisao.Motivo,
            null,
            decisao.PerfilProtegidoNaDecisao,
            decisao.DecisorTemConcessao);
}
