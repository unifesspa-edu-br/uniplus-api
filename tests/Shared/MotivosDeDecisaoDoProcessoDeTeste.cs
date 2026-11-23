namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Collections.Concurrent;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os motivos que um processo de teste escolhe para cobrir todas as decisões que as fases e as etapas dele declaram: a isenção
/// da taxa e a homologação das inscrições. O processo que não declara nenhuma dessas decisões não escolhe nenhum motivo.
/// A escolha só se faz no rascunho; o processo já publicado, que a retificação confere, leva a que foi feita antes da publicação,
/// mesmo quando a retificação o recarrega do banco: a escolha se guarda pela identidade do processo.
/// </summary>
internal static class MotivosDeDecisaoDoProcessoDeTeste
{
    private static readonly ConcurrentDictionary<Guid, IReadOnlyList<MotivoEscolhidoComOrigem>> EscolhaFeitaNoRascunho = [];

    /// <summary>A escolha que cobre o processo na versão que nasce com a publicação.</summary>
    public static MotivosEscolhidos Cobrindo(ProcessoSeletivo processo) => new(Escolha(processo), numeroDaVersaoQueNasce: 1);

    /// <summary>Os motivos escolhidos que cobrem o processo, cada um com o motivo de origem no cadastro.</summary>
    public static IReadOnlyList<MotivoEscolhidoComOrigem> Escolha(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        if (processo.MutacaoBloqueada() is not null)
        {
            return EscolhaFeitaNoRascunho.TryGetValue(processo.Id, out IReadOnlyList<MotivoEscolhidoComOrigem>? feita) ? feita : [];
        }

        IReadOnlyList<MotivoEscolhidoComOrigem> escolha = [.. MotivosDaIsencaoDeTeste.Escolha(processo), .. MotivosDaHomologacaoDeTeste.Escolha(processo)];
        EscolhaFeitaNoRascunho[processo.Id] = escolha;

        return escolha;
    }
}
