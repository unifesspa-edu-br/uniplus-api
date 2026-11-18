namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um valor que um conceito categórico admite, com o rótulo que a pessoa vê e se ele conta como aprovação.
/// Só existe conferido, dentro de um <see cref="ConceitoDaEtapa"/>.
/// </summary>
public sealed record ValorCategorico
{
    private const int CodigoMaximo = 50;
    private const int RotuloMaximo = 100;

    private ValorCategorico(string codigo, string rotulo, bool contaComoAprovacao)
    {
        Codigo = codigo;
        Rotulo = rotulo;
        ContaComoAprovacao = contaComoAprovacao;
    }

    public string Codigo { get; }
    public string Rotulo { get; }
    public bool ContaComoAprovacao { get; }

    /// <summary>
    /// Lê os textos do valor, acumulando em <paramref name="erros"/> a recusa de cada um. O texto válido é
    /// devolvido ainda que o outro seja recusado, para a repetição entre valores ser conferida.
    /// </summary>
    internal static ValorLido Ler(ValorCategoricoInformado? informado, int indice, List<FieldError> erros)
    {
        string campo = $"valores[{indice}]";
        string? codigo = TextoDoConceito.Conferir(
            informado?.Codigo, CodigoMaximo, $"{campo}.codigo", ConceitoDaEtapaErrorCodes.ValorCodigoInvalido, "O código do valor", erros);
        string? rotulo = TextoDoConceito.Conferir(
            informado?.Rotulo, RotuloMaximo, $"{campo}.rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloInvalido, "O rótulo do valor", erros);

        return new ValorLido(codigo, rotulo, informado?.ContaComoAprovacao ?? false);
    }

    /// <summary>Os textos de um valor já lidos, cada um nulo quando recusado.</summary>
    internal sealed record ValorLido(string? Codigo, string? Rotulo, bool ContaComoAprovacao)
    {
        /// <summary>O valor copiado, ou <see langword="null"/> quando algum texto foi recusado.</summary>
        internal ValorCategorico? Conferido() =>
            Codigo is null || Rotulo is null ? null : new ValorCategorico(Codigo, Rotulo, ContaComoAprovacao);
    }
}
