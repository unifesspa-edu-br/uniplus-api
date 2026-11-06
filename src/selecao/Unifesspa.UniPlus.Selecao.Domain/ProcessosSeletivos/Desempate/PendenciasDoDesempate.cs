namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

public static class PendenciasDoDesempate
{
    /// <summary>
    /// O teto de critérios e as regras do desempate por área do ENEM, como gate de publicação.
    /// Os caminhos de gravação e a restauração já os conferem; o gate é defesa em profundidade
    /// para o estado que chega por outro caminho — um rascunho gravado antes do teto, uma
    /// gravação feita fora do agregado ou uma rota nova que esqueça a conferência. Sem ele o
    /// processo publicaria, e a restauração daquela versão recusaria depois pela mesma regra.
    /// </summary>
    internal static DomainError? PendenciaDosCriteriosDesempate(EstadoDaConfiguracao estado)
    {
        if (RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(estado.CriteriosDesempate.Count) is [var excesso, ..])
        {
            return excesso.Error;
        }

        // A lista vem na precedência de sempre: forma e repetição das áreas antes do quadro. A
        // lista de áreas aceitas acompanha só a recusa contra o quadro.
        return RegrasDoDesempate.ConferirDesempatePorAreaParaPublicar(estado) is [var primeira, ..]
            ? primeira.Erro.Code == DesempatePorAreaEnemErrorCodes.ForaDoQuadro ? RegrasDoDesempate.ComAreasAceitas(primeira.Erro, estado.Classificacao) : primeira.Erro
            : null;
    }
}
