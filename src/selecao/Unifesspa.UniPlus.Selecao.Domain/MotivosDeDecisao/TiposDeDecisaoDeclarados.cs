namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Os tipos de decisão que uma fase ou etapa declara produzir: zero, um ou vários, cada um do
/// vocabulário de <see cref="TipoMotivoDeDecisaoCodigo"/>. Fase e etapa declaram o seu conjunto sem
/// herança entre elas; só a repetição dentro da mesma declaração é recusada.
/// </summary>
public static class TiposDeDecisaoDeclarados
{
    public const string Campo = "tiposDeDecisao";

    /// <summary>
    /// Confere a declaração como chegou e acumula todas as recusas, por posição de entrada
    /// (<c>{campo}[j]</c>): o elemento nulo, em branco ou fora do vocabulário é tipo inválido; o
    /// tipo válido repetido é recusado a partir da segunda ocorrência. Não altera nada.
    /// </summary>
    public static IReadOnlyList<FieldError> Validar(IReadOnlyList<string?>? bruto, string campo)
    {
        IReadOnlyList<string?> elementos = bruto ?? [];
        List<FieldError> erros = [];
        HashSet<TipoMotivoDeDecisao> jaDeclarados = [];

        for (int posicao = 0; posicao < elementos.Count; posicao++)
        {
            if (ErroDoElemento(elementos[posicao], jaDeclarados) is { } erro)
            {
                erros.Add(new FieldError($"{campo}[{posicao}]", erro));
            }
        }

        return erros;
    }

    /// <summary>
    /// Os códigos canônicos em ordem alfabética ordinal, sem repetição. Pressupõe a declaração já
    /// conferida por <see cref="Validar"/>: o que não pertence ao vocabulário fica de fora.
    /// </summary>
    public static IReadOnlyList<string> Canonicos(IReadOnlyList<string?>? bruto) =>
    [
        .. (bruto ?? [])
            .Select(TipoMotivoDeDecisaoCodigo.FromCodigo)
            .Where(static tipo => tipo != TipoMotivoDeDecisao.Nenhum)
            .Distinct()
            .Select(static tipo => tipo.ToCodigo())
            .Order(StringComparer.Ordinal),
    ];

    private static DomainError? ErroDoElemento(string? codigo, HashSet<TipoMotivoDeDecisao> jaDeclarados)
    {
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisaoCodigo.FromCodigo(codigo);
        if (tipo == TipoMotivoDeDecisao.Nenhum)
        {
            return new DomainError(
                MotivoDeDecisaoErrorCodes.TipoInvalido,
                $"O tipo informado não pertence ao vocabulário de decisões. Tipos aceitos: {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}.");
        }

        return jaDeclarados.Add(tipo)
            ? null
            : new DomainError(
                MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado,
                $"O tipo '{codigo}' é declarado mais de uma vez; cada tipo de decisão é declarado uma única vez.");
    }
}
