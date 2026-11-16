namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As regras de eliminação da classificação do grafo restaurado que citam etapa. A coerência entre ENEM e eliminação e a
/// falta em dia de prova do ENEM declarada no máximo uma vez não se repetem aqui: a classificação chega já construída por
/// <c>ConfiguracaoClassificacao.Criar</c>, e o envelope que violasse uma delas falharia na decodificação.
/// </summary>
internal static class ValidacaoDaClassificacaoRestaurada
{
    internal static DomainError? Validar(GrafoConfiguracao grafo) =>
        grafo.Classificacao.RegrasEliminacao
            .Select(regra => RecusaDaEtapaCitada(regra, grafo))
            .FirstOrDefault(static recusa => recusa is not null);

    private static DomainError? RecusaDaEtapaCitada(RegraEliminacao regra, GrafoConfiguracao grafo) =>
        regra.Args.EtapaReferenciada() is { } etapaRef && !grafo.Etapas.Any(etapa => etapa.Id == etapaRef)
            ? ReferenciaAEtapaInexistente.PelaEliminacao(etapaRef)
            : RegrasDasEtapas.AusenciaNaEtapaDoEnem(regra.Args, grafo.Etapas)
                ? RegrasDasEtapas.AusenciaNaEtapaDoEnemRecusada()
                : null;
}
