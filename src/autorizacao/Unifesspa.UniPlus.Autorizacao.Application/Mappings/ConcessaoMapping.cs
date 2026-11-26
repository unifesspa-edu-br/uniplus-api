namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Mapeamento <see cref="Concessao"/> para <see cref="ConcessaoDto"/> e para a trilha.</summary>
public static class ConcessaoMapping
{
    public static ConcessaoDto ToDto(ConcessaoComPerfil item, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(item);

        return ToDto(item.Concessao, item.PerfilNome.Valor, item.Versao, agora);
    }

    private static ConcessaoDto ToDto(Concessao c, string perfilNome, int? versao, DateTimeOffset agora) =>
        new(
            c.Id,
            new IdentidadeDto(c.BeneficiarioEmissor, c.BeneficiarioContaId, c.BeneficiarioNome),
            new PerfilResumoDto(c.PerfilId, perfilNome),
            versao,
            new EscopoDto(c.EscopoTipo.Codigo(), c.UnidadeId),
            new OrigemDto(c.OrigemTipo.Codigo(), c.OrigemId),
            c.ConcedidaEm,
            c.ValidaAte,
            c.RevogadaEm,
            RevogadaPorPessoa(c),
            c.RevogadaPelaRevisaoId,
            c.MotivoRevogacao,
            c.EstaAtiva(agora));

    /// <summary>
    /// Eventos que a própria concessão sabe contar: a concessão, o vencimento derivado da vigência no instante da leitura,
    /// que é do sistema, e a revogação. O vencimento aparece também quando a concessão vencida foi revogada depois.
    /// </summary>
    public static IEnumerable<EventoDaTrilhaDto> EventosDaConcessao(Concessao c, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(c);

        yield return EventoDaTrilhaDto.DaPessoa(
            TipoDoEventoDoHistoricoDaConcessao.Concedida,
            c.ConcedidaEm,
            new IdentidadeDto(c.ConcedidaPorEmissor, c.ConcedidaPorContaId, c.ConcedidaPorNome),
            $"Origem: {c.OrigemTipo.Codigo()} {c.OrigemId}.");

        foreach (EventoDaTrilhaDto fim in DoFim(c, agora))
        {
            yield return fim;
        }
    }

    private static IEnumerable<EventoDaTrilhaDto> DoFim(Concessao c, DateTimeOffset agora)
    {
        if (EncerramentoDaConcessao.VencimentoDe(c, agora) is { } vencidaEm)
        {
            yield return EventoDaTrilhaDto.DoSistema(TipoDoEventoDoHistoricoDaConcessao.Vencida, vencidaEm, null, "Fim da vigência.");
        }

        if (c.RevogadaEm is { } revogadaEm)
        {
            yield return RevogadaPorPessoa(c) is { } pessoa
                ? EventoDaTrilhaDto.DaPessoa(TipoDoEventoDoHistoricoDaConcessao.Revogada, revogadaEm, pessoa, c.MotivoRevogacao)
                : EventoDaTrilhaDto.DoSistema(TipoDoEventoDoHistoricoDaConcessao.RevogadaPelaMigracao, revogadaEm, null, c.MotivoRevogacao);
        }
    }

    // A revogação pela revisão das concessões é do sistema: não há pessoa a expor, e quem recusou fica na trilha da revisão.
    private static IdentidadeDto? RevogadaPorPessoa(Concessao c) =>
        c.RevogadaEm is null || c.RevogadaPelaRevisaoId is not null
            ? null
            : new IdentidadeDto(c.RevogadaPorEmissor!, c.RevogadaPorContaId!, c.RevogadaPorNome);
}
