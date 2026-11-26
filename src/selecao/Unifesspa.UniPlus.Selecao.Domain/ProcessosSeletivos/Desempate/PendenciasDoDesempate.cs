namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class PendenciasDoDesempate
{
    /// <summary>
    /// As regras do desempate por área do ENEM que dependem do quadro de pesos e da citação repetida, como gate de
    /// publicação. A forma das áreas e o teto de critérios são regras de declaração: a gravação do desempate e a
    /// reidratação as recusam, e a publicação não as repete.
    /// </summary>
    internal static DomainError? PendenciaDosCriteriosDesempate(ConfiguracaoDeclarada configuracao)
    {
        // A lista de áreas aceitas acompanha só a recusa contra o quadro.
        return RegrasDoDesempate.ConferirDesempatePorAreaParaPublicar(configuracao) is [var primeira, ..]
            ? primeira.Erro.Code == DesempatePorAreaEnemErrorCodes.ForaDoQuadro ? RegrasDoDesempate.ComAreasAceitas(primeira.Erro, configuracao.Avaliacao.Classificacao) : primeira.Erro
            : null;
    }
}
