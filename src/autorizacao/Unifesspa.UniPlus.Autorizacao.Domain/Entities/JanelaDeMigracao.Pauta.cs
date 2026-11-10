namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O que uma pessoa tem para decidir na janela, pelas mesmas regras que a gravação das decisões aplica.</summary>
public sealed partial class JanelaDeMigracao
{
    /// <summary>
    /// Quantas concessões a pessoa pode decidir na etapa agora, ou nulo quando a janela não tem nada para ela: já não está
    /// efetivamente aberta, ou a etapa não lhe deixa decisão. Ninguém decide a própria concessão e as duas etapas de uma
    /// concessão são de pessoas diferentes. Na gestão de acessos, enquanto o lote não foi enviado, a pauta é o lote
    /// inteiro, ainda que sem concessão de outra pessoa, porque enviá-lo retém as dela.
    /// </summary>
    /// <param name="etapa">A etapa que a pessoa exerce: a gestão de acessos ou a proteção de dados.</param>
    /// <param name="quem">A pessoa que lê.</param>
    /// <param name="concessoes">As concessões ativas do perfil.</param>
    /// <param name="agora">O instante da leitura.</param>
    public int? QuantidadeADecidir(EtapaDaMigracao etapa, Identidade quem, IReadOnlyList<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(quem);
        ArgumentNullException.ThrowIfNull(concessoes);

        if (!EstaAberta(agora, Pendencias(concessoes, agora)))
        {
            return null;
        }

        List<Concessao> deOutros = [.. concessoes.Where(c => Elegivel(c, agora) && c.Beneficiario != quem)];
        Dictionary<Guid, SituacaoDaMigracao> situacoes = _migracoes.ToDictionary(static m => m.ConcessaoId, static m => m.Situacao);
        int quantidade = etapa == EtapaDaMigracao.ProtecaoDeDados
            ? DaProtecao(deOutros, situacoes, ConferenciasDaMigracao.ConcessoesDecididasNaGestaoPor(this, quem))
            : DaGestao(deOutros, situacoes);

        return quantidade > 0 || (etapa == EtapaDaMigracao.GestaoDeAcessos && LoteDaGestao is null) ? quantidade : null;
    }

    private int DaGestao(List<Concessao> deOutros, Dictionary<Guid, SituacaoDaMigracao> situacoes) =>
        LoteDaGestao is null ? deOutros.Count : deOutros.Count(c => situacoes.GetValueOrDefault(c.Id) == SituacaoDaMigracao.Retida);

    private static int DaProtecao(
        List<Concessao> deOutros, Dictionary<Guid, SituacaoDaMigracao> situacoes, HashSet<Guid> decididasPelaPessoa) =>
        deOutros.Count(c =>
            situacoes.GetValueOrDefault(c.Id) == SituacaoDaMigracao.AguardandoProtecaoDeDados && !decididasPelaPessoa.Contains(c.Id));
}
