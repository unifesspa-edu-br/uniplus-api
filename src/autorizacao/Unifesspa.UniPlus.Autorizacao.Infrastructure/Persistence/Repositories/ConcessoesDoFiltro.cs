namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>O recorte das concessões pelos critérios da listagem: pessoa, unidade, perfil e situação.</summary>
internal static class ConcessoesDoFiltro
{
    /// <summary>As concessões que atendem a todos os critérios informados no filtro; o critério ausente não recorta.</summary>
    public static IQueryable<Concessao> Filtradas(this IQueryable<Concessao> concessoes, FiltroDeConcessoes filtro)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(filtro);

        return concessoes
            .DaPessoa(filtro.PessoaEmissor, filtro.PessoaContaId)
            .NaUnidade(filtro.UnidadeId)
            .DoPerfil(filtro.PerfilId)
            .NaSituacao(filtro.Situacao, filtro.Agora);
    }

    // Só recorta com o emissor e a conta informados: um dos dois sozinho não identifica a pessoa. O texto vazio conta como
    // informado.
    private static IQueryable<Concessao> DaPessoa(this IQueryable<Concessao> concessoes, string? emissor, string? contaId) =>
        emissor is not null && contaId is not null
            ? concessoes.Where(c => c.BeneficiarioEmissor == emissor && c.BeneficiarioContaId == contaId)
            : concessoes;

    private static IQueryable<Concessao> NaUnidade(this IQueryable<Concessao> concessoes, Guid? unidadeId) =>
        unidadeId is { } unidade ? concessoes.Where(c => c.UnidadeId == unidade) : concessoes;

    private static IQueryable<Concessao> DoPerfil(this IQueryable<Concessao> concessoes, Guid? perfilId) =>
        perfilId is { } perfil ? concessoes.Where(c => c.PerfilId == perfil) : concessoes;

    private static IQueryable<Concessao> NaSituacao(this IQueryable<Concessao> concessoes, SituacaoDaConcessao? situacao, DateTimeOffset agora) =>
        situacao switch
        {
            SituacaoDaConcessao.Ativa => concessoes.Vigentes(agora),
            SituacaoDaConcessao.Revogada => concessoes.Where(c => c.RevogadaEm != null),
            SituacaoDaConcessao.Vencida => concessoes.Where(c => c.RevogadaEm == null && c.ValidaAte != null && c.ValidaAte <= agora),
            null => concessoes,
            _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "Situação da concessão desconhecida."),
        };
}
