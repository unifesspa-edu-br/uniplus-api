namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O grafo de precedências vigente, montado com as arestas vivas do cadastro no momento da escrita. Só lê:
/// diz se uma nova aresta repete um par que já existe ou fecha um ciclo, sem consultar o banco (ADR-0042).
/// </summary>
internal sealed class GrafoDePrecedencias
{
    private readonly IReadOnlyList<PrecedenciaFase> _arestasVivas;

    internal GrafoDePrecedencias(IReadOnlyList<PrecedenciaFase> arestasVivas)
    {
        _arestasVivas = arestasVivas;
    }

    internal Result Aceita(string antecessora, string sucessora)
    {
        if (JaTemOPar(antecessora, sucessora))
        {
            return Result.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.ArestaDuplicada,
                "Já existe uma aresta de precedência viva com este par de fases."));
        }

        return FechariaCiclo(antecessora, sucessora)
            ? Result.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.CicloDetectado,
                "A aresta informada fecharia um ciclo no grafo de precedências."))
            : Result.Success();
    }

    private bool JaTemOPar(string antecessora, string sucessora) =>
        Sucessoras(antecessora).Contains(sucessora, StringComparer.Ordinal);

    // Fechar ciclo é já existir, no grafo vigente, um caminho da sucessora de volta à antecessora.
    private bool FechariaCiclo(string antecessora, string sucessora) =>
        Alcancaveis(sucessora).Contains(antecessora);

    // Busca em profundidade a partir de um código; o próprio código conta como alcançado.
    private HashSet<string> Alcancaveis(string origem)
    {
        HashSet<string> visitados = new(StringComparer.Ordinal);
        Stack<string> pilha = new([origem]);

        while (pilha.TryPop(out string? atual))
        {
            if (visitados.Add(atual))
            {
                EmpilharSucessoras(pilha, atual);
            }
        }

        return visitados;
    }

    private void EmpilharSucessoras(Stack<string> pilha, string codigo)
    {
        foreach (string sucessora in Sucessoras(codigo))
        {
            pilha.Push(sucessora);
        }
    }

    private IEnumerable<string> Sucessoras(string codigo) =>
        _arestasVivas
            .Where(a => string.Equals(a.AntecessoraCodigo, codigo, StringComparison.Ordinal))
            .Select(a => a.SucessoraCodigo);
}
