namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>Junta o que cada item declarado montou e o que cada um recusou, para recusar todos de uma vez (ADR-0125).</summary>
internal sealed class RecusasAcumuladas<T>
{
    private readonly List<T> _montados = [];
    private readonly List<FieldError> _recusas = [];

    internal void Juntar(Result<T> resultado)
    {
        if (resultado.IsSuccess)
        {
            _montados.Add(resultado.Value!);
        }
        else
        {
            _recusas.AddRange(resultado.Errors);
        }
    }

    internal Result<List<T>> Resultado() => _recusas.Count > 0
        ? Result<List<T>>.ValidationFailure(_recusas)
        : Result<List<T>>.Success(_montados);
}
