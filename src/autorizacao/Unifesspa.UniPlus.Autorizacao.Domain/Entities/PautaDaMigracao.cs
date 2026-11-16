namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O que uma pessoa tem para decidir na janela, pelas mesmas regras que a gravação das decisões aplica.</summary>
public static class PautaDaMigracao
{
    /// <summary>
    /// Quantas concessões a pessoa pode decidir na etapa agora, ou nulo quando a janela não tem nada para ela: já não está
    /// efetivamente aberta, ou a etapa não lhe deixa decisão. Ninguém decide a própria concessão e as duas etapas de uma
    /// concessão são de pessoas diferentes. Na gestão de acessos, enquanto o lote não foi enviado, a pauta é o lote
    /// inteiro, ainda que sem concessão de outra pessoa, porque enviá-lo retém as dela.
    /// </summary>
    /// <param name="janela">A janela de migração que se lê.</param>
    /// <param name="etapa">A etapa que a pessoa exerce: a gestão de acessos ou a proteção de dados.</param>
    /// <param name="quem">A pessoa que lê.</param>
    /// <param name="concessoes">As concessões ativas do perfil.</param>
    /// <param name="agora">O instante da leitura.</param>
    public static int? QuantidadeADecidir(
        JanelaDeMigracao janela, EtapaDaMigracao etapa, Identidade quem, IReadOnlyList<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(quem);
        ArgumentNullException.ThrowIfNull(concessoes);

        if (!janela.EstaAberta(agora, janela.Pendencias(concessoes, agora)))
        {
            return null;
        }

        int quantidade = Contar(janela, etapa, quem, [.. concessoes.Where(c => janela.Elegivel(c, agora) && c.Beneficiario != quem)]);
        return quantidade > 0 || (etapa == EtapaDaMigracao.GestaoDeAcessos && janela.LoteDaGestao is null) ? quantidade : null;
    }

    private static int Contar(JanelaDeMigracao janela, EtapaDaMigracao etapa, Identidade quem, List<Concessao> deOutros)
    {
        Dictionary<Guid, SituacaoDaMigracao> situacoes = janela.Migracoes.ToDictionary(static m => m.ConcessaoId, static m => m.Situacao);

        return etapa == EtapaDaMigracao.ProtecaoDeDados
            ? DaProtecao(deOutros, situacoes, ConferenciasDaMigracao.ConcessoesDecididasNaGestaoPor(janela, quem))
            : DaGestao(janela, deOutros, situacoes);
    }

    private static int DaGestao(JanelaDeMigracao janela, List<Concessao> deOutros, Dictionary<Guid, SituacaoDaMigracao> situacoes) =>
        janela.LoteDaGestao is null ? deOutros.Count : deOutros.Count(c => situacoes.GetValueOrDefault(c.Id) == SituacaoDaMigracao.Retida);

    private static int DaProtecao(
        List<Concessao> deOutros, Dictionary<Guid, SituacaoDaMigracao> situacoes, HashSet<Guid> decididasPelaPessoa) =>
        deOutros.Count(c =>
            situacoes.GetValueOrDefault(c.Id) == SituacaoDaMigracao.AguardandoProtecaoDeDados && !decididasPelaPessoa.Contains(c.Id));
}
