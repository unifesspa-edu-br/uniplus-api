namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Mapeamento da <see cref="RevisaoDeAcessos"/> para os DTOs e para os eventos da trilha da concessão.</summary>
public static class RevisaoMapping
{
    public const string EmAndamento = SituacaoDaRevisaoCodigo.EmAndamento;
    public const string Concluida = SituacaoDaRevisaoCodigo.Concluida;

    public static RevisaoResumoDto ToResumo(ResumoDaRevisao resumo)
    {
        ArgumentNullException.ThrowIfNull(resumo);

        RevisaoDeAcessos r = resumo.Revisao;

        return new RevisaoResumoDto(
            r.Id, r.AbertaEm, new IdentidadeDto(r.AbertaPorEmissor, r.AbertaPorSub, r.AbertaPorNome),
            Situacao(resumo.Pendentes), resumo.TotalDeItens, resumo.Pendentes);
    }

    /// <param name="detalhada">A revisão com os itens.</param>
    /// <param name="representantes">Quem é Representante do controlador no instante da leitura, de que depende quem decide o item pendente.</param>
    /// <param name="leitor">Quem lê, para marcar os itens da concessão dele; nulo quando o token não o identifica.</param>
    public static RevisaoDeAcessosDto ToDto(RevisaoDetalhada detalhada, IReadOnlySet<Identidade> representantes, Identidade? leitor)
    {
        ArgumentNullException.ThrowIfNull(detalhada);
        ArgumentNullException.ThrowIfNull(representantes);

        RevisaoDeAcessos r = detalhada.Revisao;
        HashSet<Guid> ativas = [.. detalhada.Itens.Where(i => i.ConcessaoAtiva).Select(i => i.Concessao.Id)];
        int pendentes = r.Pendentes(ativas);

        return new RevisaoDeAcessosDto(
            r.Id, r.AbertaEm, new IdentidadeDto(r.AbertaPorEmissor, r.AbertaPorSub, r.AbertaPorNome),
            Situacao(pendentes), detalhada.Itens.Count, pendentes,
            [.. detalhada.Itens.Select(i => ToDto(i, representantes, leitor))]);
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

    private static string Situacao(ItemDaRevisaoComConcessao item) =>
        item.Item.Resultado?.ToCodigo() ?? (item.ConcessaoAtiva ? "pendente" : "concessao_encerrada");

    // O item decidido mostra a governança gravada na decisão; o pendente, a que o decidiria agora.
    private static TipoDeGovernanca QuemDecide(ItemDaRevisao item, Concessao concessao, IReadOnlySet<Identidade> representantes) =>
        item.DecididoComo
        ?? ItemDaRevisao.QuemDecide(representantes.Contains(new Identidade(concessao.BeneficiarioEmissor, concessao.BeneficiarioSub)));

    private static ItemDaRevisaoDto ToDto(ItemDaRevisaoComConcessao item, IReadOnlySet<Identidade> representantes, Identidade? leitor)
    {
        Concessao c = item.Concessao;
        ItemDaRevisao i = item.Item;
        return new ItemDaRevisaoDto(
            i.Id,
            c.Id,
            new IdentidadeDto(c.BeneficiarioEmissor, c.BeneficiarioSub, c.BeneficiarioNome),
            new PerfilResumoDto(c.PerfilId, item.PerfilNome),
            new EscopoDto(c.EscopoTipo.ToCodigo(), c.UnidadeId),
            c.ValidaAte,
            QuemDecide(i, c, representantes).ToCodigo(),
            Situacao(item),
            i.DecididoPor is { } p ? new IdentidadeDto(p.Emissor, p.Sub, p.Nome) : null,
            i.DecididoEm,
            EhDoLeitor(c, leitor));
    }

    private static bool EhDoLeitor(Concessao concessao, Identidade? leitor) =>
        leitor is not null && leitor == new Identidade(concessao.BeneficiarioEmissor, concessao.BeneficiarioSub);
}
