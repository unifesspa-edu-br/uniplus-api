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
    /// Eventos que a própria concessão sabe contar: a concessão, o vencimento derivado da vigência no instante da leitura,
    /// que é do sistema, e a revogação. O vencimento aparece também quando a concessão vencida foi revogada depois.
    /// </summary>
    public static IEnumerable<EventoDaTrilhaDto> EventosDaConcessao(Concessao c, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(c);

        yield return new EventoDaTrilhaDto(
            "concedida",
            c.ConcedidaEm,
            new IdentidadeDto(c.ConcedidaPorEmissor, c.ConcedidaPorSub, c.ConcedidaPorNome),
            $"Origem: {c.OrigemTipo.ToCodigo()} {c.OrigemId}.");

        foreach (EventoDaTrilhaDto fim in DoFim(c, agora))
        {
            yield return fim;
        }
    }

    private static IEnumerable<EventoDaTrilhaDto> DoFim(Concessao c, DateTimeOffset agora)
    {
        if (EncerramentoDaConcessao.VencimentoDe(c, agora) is { } vencidaEm)
        {
            yield return new("vencida", vencidaEm, null, "Fim da vigência.", "sistema");
        }

        if (c.RevogadaEm is { } revogadaEm)
        {
            yield return c.RevogadaPelaJanelaId is null
                ? new EventoDaTrilhaDto("revogada", revogadaEm, RevogadaPorPessoa(c), c.MotivoRevogacao)
                : new EventoDaTrilhaDto("revogada_pela_migracao", revogadaEm, null, c.MotivoRevogacao, "sistema");
        }
    }

    // A revogação pela janela de migração é do sistema: não há pessoa a expor, e quem recusou fica na trilha da janela.
    private static IdentidadeDto? RevogadaPorPessoa(Concessao c) =>
        c.RevogadaEm is null || c.RevogadaPelaJanelaId is not null
            ? null
            : new IdentidadeDto(c.RevogadaPorEmissor!, c.RevogadaPorSub!, c.RevogadaPorNome);
}
