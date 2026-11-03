namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

internal static class JanelaDaEtapaNaFase
{
    /// <summary>
    /// As fases indexadas pelo código canônico, resolvendo a primeira quando duas o
    /// repetem — a mesma escolha que a resolução do vínculo etapa × fase sempre fez.
    /// </summary>
    internal static Dictionary<string, FaseCronograma> FasesPorCodigo(IEnumerable<FaseCronograma> fases)
    {
        Dictionary<string, FaseCronograma> porCodigo = new(StringComparer.Ordinal);
        foreach (FaseCronograma fase in fases)
        {
            porCodigo.TryAdd(fase.Codigo, fase);
        }

        return porCodigo;
    }

    /// <summary>
    /// A primeira etapa cuja janela própria não cabe na janela da fase que a abriga, ou
    /// <see langword="null"/> quando toda etapa datada acontece dentro da fase que a hospeda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a raiz quem confere, pelo mesmo motivo que só ela resolve o vínculo: a etapa não
    /// enxerga o cronograma e a fase não enxerga as etapas. Sem esta invariante o certame é
    /// publicável com uma prova marcada para antes de a fase que a contém começar, e o
    /// envelope congela a incoerência.
    /// </para>
    /// <para>
    /// Só compara o que os dois lados declararam. Fase sem janela não contém nada — exigir
    /// data dela por causa da etapa inventaria obrigação que o cadastro não faz —, e etapa
    /// sem janela acontece na janela da fase, que é o que a tela promete. Bordas coincidentes
    /// passam: começar junto com a fase é o caso comum. A etapa que declara só um dos
    /// extremos herda o outro da fase, e por isso o extremo declarado é conferido contra os
    /// DOIS extremos da fase: uma prova que só diz "começa em 20 de março", numa fase que
    /// terminou em 10, acontece inteira fora dela sem violar nenhuma das duas comparações
    /// homônimas.
    /// </para>
    /// <para>
    /// A etapa que declara uma fase ausente do conjunto é ignorada aqui: na gravação das
    /// etapas ela já foi recusada por <c>EtapaSemFaseNoCronograma</c>, na gravação do
    /// cronograma ela sai na poda em cascata, e na hidratação do EF não há a quem compará-la.
    /// </para>
    /// </remarks>
    internal static DomainError? ViolacaoDaJanelaDaEtapaNaFase(
        IReadOnlyCollection<EtapaProcesso> etapas,
        Dictionary<string, FaseCronograma> fasesPorCodigo)
    {
        foreach (EtapaProcesso etapa in etapas)
        {
            if (etapa.FaseCodigo is not { } codigoDaFase
                || !fasesPorCodigo.TryGetValue(codigoDaFase, out FaseCronograma? fase))
            {
                continue;
            }

            if (etapa.Inicio is { } inicioDaEtapa)
            {
                if (fase.Inicio is { } inicioDaFase && inicioDaEtapa < inicioDaFase)
                {
                    return new DomainError(
                        "ProcessoSeletivo.EtapaComecaAntesDaFase",
                        $"A etapa \"{etapa.Nome}\" começa em {inicioDaEtapa:O}, antes da fase {codigoDaFase} ({inicioDaFase:O}) — a etapa acontece dentro da fase.");
                }

                if (fase.Fim is { } fimDaFaseParaOInicio && inicioDaEtapa > fimDaFaseParaOInicio)
                {
                    return new DomainError(
                        "ProcessoSeletivo.EtapaForaDaJanelaDaFase",
                        $"A etapa \"{etapa.Nome}\" começa em {inicioDaEtapa:O}, depois de a fase {codigoDaFase} ter terminado ({fimDaFaseParaOInicio:O}) — a etapa acontece dentro da fase.");
                }
            }

            if (etapa.Fim is { } fimDaEtapa)
            {
                if (fase.Fim is { } fimDaFase && fimDaEtapa > fimDaFase)
                {
                    return new DomainError(
                        "ProcessoSeletivo.EtapaTerminaDepoisDaFase",
                        $"A etapa \"{etapa.Nome}\" termina em {fimDaEtapa:O}, depois da fase {codigoDaFase} ({fimDaFase:O}) — a etapa acontece dentro da fase.");
                }

                if (fase.Inicio is { } inicioDaFaseParaOFim && fimDaEtapa < inicioDaFaseParaOFim)
                {
                    return new DomainError(
                        "ProcessoSeletivo.EtapaForaDaJanelaDaFase",
                        $"A etapa \"{etapa.Nome}\" termina em {fimDaEtapa:O}, antes de a fase {codigoDaFase} ter começado ({inicioDaFaseParaOFim:O}) — a etapa acontece dentro da fase.");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A primeira etapa fora da janela da fase que a abriga no cronograma corrente, ou
    /// <see langword="null"/> quando toda etapa datada acontece dentro da fase que a hospeda.
    /// </summary>
    /// <remarks>
    /// O gate devolve o erro; <see cref="AvaliarConformidade"/> projeta o mesmo predicado num
    /// item do checklist. As duas metades vêm da MESMA travessia
    /// (<see cref="ViolacaoDaJanelaDaEtapaNaFase"/>), que é a que as duas gravações chamam —
    /// é o que mantém checklist e recusa bicondicionais.
    /// </remarks>
    internal static DomainError? PendenciaDaJanelaDaEtapaNaFase(EstadoDaConfiguracao estado) =>
        ViolacaoDaJanelaDaEtapaNaFase(estado.Etapas, FasesPorCodigo(estado.CronogramaFases));
}
