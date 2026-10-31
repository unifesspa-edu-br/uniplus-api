namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>Mapeamento da <see cref="RevisaoDeAcessos"/> para os DTOs e para os eventos da trilha da concessão.</summary>
public static class RevisaoMapping
{
    public const string EmAndamento = "em_andamento";
    public const string Concluida = "concluida";

    public static RevisaoResumoDto ToResumo(ResumoDaRevisao resumo)
    {
        ArgumentNullException.ThrowIfNull(resumo);

        RevisaoDeAcessos r = resumo.Revisao;

        return new RevisaoResumoDto(
            r.Id, r.AbertaEm, new IdentidadeDto(r.AbertaPorEmissor, r.AbertaPorSub, r.AbertaPorNome),
            Situacao(resumo.Pendentes), resumo.TotalDeItens, resumo.Pendentes);
    }

    public static RevisaoDeAcessosDto ToDto(RevisaoDetalhada detalhada)
    {
        ArgumentNullException.ThrowIfNull(detalhada);

        RevisaoDeAcessos r = detalhada.Revisao;
        HashSet<Guid> ativas = [.. detalhada.Itens.Where(i => i.ConcessaoAtiva).Select(i => i.Concessao.Id)];
        int pendentes = r.Pendentes(ativas);

        return new RevisaoDeAcessosDto(
            r.Id,
            r.AbertaEm,
            new IdentidadeDto(r.AbertaPorEmissor, r.AbertaPorSub, r.AbertaPorNome),
            Situacao(pendentes),
            detalhada.Itens.Count,
            pendentes,
            [.. detalhada.Itens.Select(ToDto)]);
    }

    public static EventoDaTrilhaDto ToEvento(ItemDaRevisao item)
    {
        ArgumentNullException.ThrowIfNull(item);

        bool revogada = item.Resultado == ResultadoDoItemDaRevisao.Revogada;

        return new EventoDaTrilhaDto(
            revogada ? "revisao_revogada" : "revisao_confirmada",
            item.DecididoEm!.Value,
            new IdentidadeDto(item.DecididoPorEmissor!, item.DecididoPorSub!, item.DecididoPorNome),
            revogada ? $"Revisão {item.RevisaoId}: acesso revogado." : $"Revisão {item.RevisaoId}: acesso mantido.");
    }

    private static string Situacao(int pendentes) => pendentes > 0 ? EmAndamento : Concluida;

    private static ItemDaRevisaoDto ToDto(ItemDaRevisaoComConcessao item)
    {
        Concessao c = item.Concessao;
        ItemDaRevisao i = item.Item;

        string situacao = i.Resultado?.ToCodigo() ?? (item.ConcessaoAtiva ? "pendente" : "concessao_encerrada");

        return new ItemDaRevisaoDto(
            i.Id,
            c.Id,
            new IdentidadeDto(c.BeneficiarioEmissor, c.BeneficiarioSub, c.BeneficiarioNome),
            new PerfilResumoDto(c.PerfilId, item.PerfilNome),
            new EscopoDto(c.EscopoTipo.ToCodigo(), c.UnidadeId),
            c.ValidaAte,
            i.Decide.ToCodigo(),
            situacao,
            i.DecididoPor is { } p ? new IdentidadeDto(p.Emissor, p.Sub, p.Nome) : null,
            i.DecididoEm);
    }
}
