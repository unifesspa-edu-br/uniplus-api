namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDaClassificacaoRestaurada
{
    // A coerência entre o ENEM e as regras de eliminação, e a falta em dia de prova do ENEM declarada
    // no máximo uma vez, não se repetem aqui: a classificação do grafo chega já construída por
    // ConfiguracaoClassificacao.Criar, e um envelope que violasse uma delas teria falhado na
    // decodificação, antes da validação do grafo.
    internal static DomainError? Recusa(GrafoConfiguracao grafo)
    {
        foreach (RegraEliminacao regra in grafo.Classificacao.RegrasEliminacao)
        {
            if (RegrasDaClassificacao.RecusaDaEtapaDaEliminacao(regra, grafo.Etapas) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }
}
