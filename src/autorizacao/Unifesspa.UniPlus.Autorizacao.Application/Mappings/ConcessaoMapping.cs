namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Mapeamento <see cref="Concessao"/> para <see cref="ConcessaoDto"/> e para a trilha.</summary>
public static class ConcessaoMapping
{
    public static ConcessaoDto ToDto(ConcessaoComPerfil item, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(item);

        Concessao c = item.Concessao;

        return new ConcessaoDto(
            c.Id,
            new IdentidadeDto(c.BeneficiarioEmissor, c.BeneficiarioSub, c.BeneficiarioNome),
            new PerfilResumoDto(c.PerfilId, item.PerfilNome),
            new EscopoDto(c.EscopoTipo.ToCodigo(), c.UnidadeId),
            new OrigemDto(c.OrigemTipo.ToCodigo(), c.OrigemId),
            c.ConcedidaEm,
            c.ValidaAte,
            c.RevogadaEm,
            c.RevogadaEm is null ? null : new IdentidadeDto(c.RevogadaPorEmissor!, c.RevogadaPorSub!, c.RevogadaPorNome),
            c.MotivoRevogacao,
            c.EstaAtiva(agora));
    }

    /// <summary>Eventos que a própria concessão sabe contar: a concessão e a revogação.</summary>
    public static IEnumerable<EventoDaTrilhaDto> EventosDaConcessao(Concessao c)
    {
        ArgumentNullException.ThrowIfNull(c);

        yield return new EventoDaTrilhaDto(
            "concedida",
            c.ConcedidaEm,
            new IdentidadeDto(c.ConcedidaPorEmissor, c.ConcedidaPorSub, c.ConcedidaPorNome),
            $"Origem: {c.OrigemTipo.ToCodigo()} {c.OrigemId}.");

        if (c.RevogadaEm is { } revogadaEm)
        {
            yield return new EventoDaTrilhaDto(
                "revogada",
                revogadaEm,
                new IdentidadeDto(c.RevogadaPorEmissor!, c.RevogadaPorSub!, c.RevogadaPorNome),
                c.MotivoRevogacao);
        }
    }
}
