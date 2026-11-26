namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra dos textos que o conceito copia do catálogo: obrigatórios, aparados, em NFC e dentro do tamanho
/// da coluna. A mensagem de recusa nunca repete o texto recusado.
/// </summary>
internal static class TextoDoConceito
{
    /// <summary>O texto normalizado, ou <see langword="null"/> com a recusa acumulada em <paramref name="erros"/>.</summary>
    internal static string? Conferir(
        string? texto, int maximo, string caminho, string codigoDoErro, string descricao, List<FieldError> erros)
    {
        string? normalizado = TextoCongelado.NormalizarOuNulo(texto);
        if (normalizado is not null && TextoCongelado.CabeNaColuna(normalizado, maximo))
        {
            return normalizado;
        }

        erros.Add(new(caminho, new DomainError(
            codigoDoErro,
            $"{descricao} é obrigatório, deve ter no máximo {maximo} caracteres e não pode conter caractere inválido.")));
        return null;
    }
}
