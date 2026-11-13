namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Mapeamento <see cref="Concessao"/> para <see cref="ConcessaoDto"/> e para a trilha.</summary>
public static class ConcessaoMapping
{
    public static ConcessaoDto ToDto(ConcessaoComPerfil item, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(item);

        return ToDto(item.Concessao, item.PerfilNome, item.Versao, agora);
    }

    private static ConcessaoDto ToDto(Concessao c, string perfilNome, int? versao, DateTimeOffset agora) =>
        new(
            c.Id,
            new IdentidadeDto(c.BeneficiarioEmissor, c.BeneficiarioSub, c.BeneficiarioNome),
            new PerfilResumoDto(c.PerfilId, perfilNome),
            versao,
            new EscopoDto(c.EscopoTipo.ToCodigo(), c.UnidadeId),
            new OrigemDto(c.OrigemTipo.ToCodigo(), c.OrigemId),
            c.ConcedidaEm,
            c.ValidaAte,
            c.RevogadaEm,
            RevogadaPorPessoa(c),
            c.RevogadaPelaJanelaId,
            c.MotivoRevogacao,
            c.EstaAtiva(agora));

    /// <summary>
    /// Eventos que a própria concessão sabe contar: a concessão e o que a encerrou, a revogação ou, derivado da vigência no
    /// instante da leitura, o vencimento, que é do sistema.
    /// </summary>
    public static IEnumerable<EventoDaTrilhaDto> EventosDaConcessao(Concessao c, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(c);

        yield return new EventoDaTrilhaDto(
            "concedida",
            c.ConcedidaEm,
            new IdentidadeDto(c.ConcedidaPorEmissor, c.ConcedidaPorSub, c.ConcedidaPorNome),
            $"Origem: {c.OrigemTipo.ToCodigo()} {c.OrigemId}.");

        if (EventoDoEncerramento(c, agora) is { } encerramento)
        {
            yield return encerramento;
        }
    }

    private static EventoDaTrilhaDto? EventoDoEncerramento(Concessao c, DateTimeOffset agora) =>
        EncerramentoDaConcessao.De(c, agora) switch
        {
            null => null,
            { Causa: CausaDoEncerramento.Vencida, Em: var vencidaEm } => new("vencida", vencidaEm, null, "Fim da vigência.", "sistema"),
            { Em: var revogadaEm } when c.RevogadaPelaJanelaId is not null => new("revogada_pela_migracao", revogadaEm, null, c.MotivoRevogacao, "sistema"),
            { Em: var revogadaEm } => new("revogada", revogadaEm, RevogadaPorPessoa(c), c.MotivoRevogacao),
        };

    // A revogação pela janela de migração é do sistema: não há pessoa a expor, e quem recusou fica na trilha da janela.
    private static IdentidadeDto? RevogadaPorPessoa(Concessao c) =>
        c.RevogadaEm is null || c.RevogadaPelaJanelaId is not null
            ? null
            : new IdentidadeDto(c.RevogadaPorEmissor!, c.RevogadaPorSub!, c.RevogadaPorNome);
}
