namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas de um elemento do pedido, lidas todas antes de decidir. Todo caminho é relativo ao elemento: quem o
/// contém junta o seu próprio caminho. A recusa sem campo aponta o próprio elemento quando ele está dentro de outro,
/// e fica sem campo quando o elemento é a raiz.
/// </summary>
internal sealed class RecusasDoElemento
{
    private readonly List<FieldError> _recusas = [];

    internal IReadOnlyList<FieldError> Recusas => _recusas;

    internal T? Parte<T>(Result<T> lida, string campo)
    {
        Aceitar(lida, campo, out T? valor);
        return valor;
    }

    internal List<TSaida> Lista<TEntrada, TSaida>(
        IReadOnlyList<TEntrada?>? lista, string campo, Func<TEntrada?, Result<TSaida>> ler)
    {
        List<TSaida> lidos = [];
        for (int posicao = 0; posicao < (lista?.Count ?? 0); posicao++)
        {
            if (Aceitar(ler(lista![posicao]), $"{campo}[{posicao}]", out TSaida? lido))
            {
                lidos.Add(lido!);
            }
        }

        return lidos;
    }

    internal void Recusar(DomainError? recusa, string? campo = null)
    {
        if (recusa is null)
        {
            return;
        }

        FieldError erro = new(null, recusa);
        _recusas.Add(campo is null ? erro : erro.NoCaminhoDoItem(campo));
    }

    internal Result<T> Montado<T>(Func<T> montar) =>
        _recusas.Count > 0 ? Result<T>.ValidationFailure(_recusas) : Result<T>.Success(montar());

    /// <summary>Confere o elemento só se nada foi recusado antes, porque a conferência depende das partes lidas.</summary>
    internal Result<T> ConferidoPor<T>(Func<Result<T>> conferir) =>
        _recusas.Count > 0 ? Result<T>.ValidationFailure(_recusas) : conferir();

    private bool Aceitar<T>(Result<T> lida, string campo, out T? valor)
    {
        valor = lida.Value;
        if (lida.IsFailure)
        {
            _recusas.AddRange(lida.Errors.Select(erro => erro.NoCaminhoDoItem(campo)));
        }

        return lida.IsSuccess;
    }
}
