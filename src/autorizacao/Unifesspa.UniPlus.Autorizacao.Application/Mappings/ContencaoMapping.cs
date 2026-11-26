namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Mapeamento da trilha da contenção para os registros que a API expõe, na ordem total da trilha.</summary>
public static class ContencaoMapping
{
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
            CodigoComRotulo<TipoDoRegistroDoBloqueio>.De(DoTipo(evento.Tipo)),
            (evento.SuspensaoId ?? evento.DesativacaoId)!.Value,
            evento.OrdemNoHistorico,
            evento.Instante,
            CodigoComRotulo<AtorDoEventoDaContencao>.De(evento.Ator),
            evento.AtorEmissor is null || evento.AtorContaId is null ? null : new IdentidadeDto(evento.AtorEmissor, evento.AtorContaId, evento.AtorNome),
            evento.Papel is { } papel ? CodigoComRotulo<EtapaDaVersao>.De(papel) : null,
            evento.Motivo,
            evento.Origem is { } origem ? CodigoComRotulo<OrigemDaRetomada>.De(origem) : null,
            evento.PerfilProtegido,
            null);

    private static RegistroDaContencaoDto DaDecisao(DecisaoDaRetomada decisao) =>
        new(
            decisao.Id,
            CodigoComRotulo<TipoDoRegistroDoBloqueio>.De(TipoDoRegistroDoBloqueio.DecisaoDaRetomada),
            decisao.SuspensaoId,
            decisao.OrdemNoHistorico,
            decisao.Instante,
            CodigoComRotulo<AtorDoEventoDaContencao>.De(AtorDoEventoDaContencao.Humano),
            new IdentidadeDto(decisao.DecisorEmissor, decisao.DecisorContaId, decisao.DecisorNome),
            CodigoComRotulo<EtapaDaVersao>.De(decisao.Etapa),
            decisao.Motivo,
            null,
            decisao.PerfilProtegidoNaDecisao,
            decisao.DecisorTemConcessao);

    private static TipoDoRegistroDoBloqueio DoTipo(TipoDoEventoDaContencao tipo) => tipo switch
    {
        TipoDoEventoDaContencao.Suspensao => TipoDoRegistroDoBloqueio.Suspensao,
        TipoDoEventoDaContencao.Retomada => TipoDoRegistroDoBloqueio.Retomada,
        TipoDoEventoDaContencao.Desativacao => TipoDoRegistroDoBloqueio.Desativacao,
        TipoDoEventoDaContencao.Reativacao => TipoDoRegistroDoBloqueio.Reativacao,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };
}
