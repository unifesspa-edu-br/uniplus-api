namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Diagnostics;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Reduz um quadro fixado pelo edital cuja soma passa das vagas ofertadas base, aplicando os parâmetros de ajuste do quadro
/// que a regra de ajuste declara. Serviço de domínio puro: recebe o quadro já resolvido e
/// não consulta cadastro (ADR-0013).
/// </summary>
public static class ReconciliadorQuadroInstitucional
{
    /// <param name="Quantidades">Quadro após a redução, na mesma ordem de entrada.</param>
    /// <param name="Reduzido">Total retirado — o que o contrato publica como estouro absorvido.</param>
    public sealed record QuadroReconciliado(IReadOnlyList<int> Quantidades, int Reduzido);

    /// <summary>
    /// Retira <paramref name="excesso"/> vagas do quadro, distribuindo conforme a regra de ajuste do quadro.
    /// </summary>
    /// <remarks>
    /// Recusa quando as modalidades que a regra de ajuste do quadro nomeia não existem no quadro ou não somam o
    /// excesso: reduzir o que se pode e devolver um quadro que continua estourado seria
    /// entregar como reconciliado o que não fecha.
    /// </remarks>
    public static Result<QuadroReconciliado> Reduzir(
        IReadOnlyList<string> codigos, IReadOnlyList<int> quantidades, int excesso, ParametrosRegraAjusteDistribuicao parametros)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(quantidades);
        ArgumentNullException.ThrowIfNull(parametros);

        return excesso <= 0
            ? Result<QuadroReconciliado>.Success(new QuadroReconciliado(quantidades, Reduzido: 0))
            : ReduzirConformeARegraDeAjuste(codigos, quantidades, excesso, parametros);
    }

    private static Result<QuadroReconciliado> ReduzirConformeARegraDeAjuste(
        IReadOnlyList<string> codigos, IReadOnlyList<int> quantidades, int excesso, ParametrosRegraAjusteDistribuicao parametros) =>
        parametros switch
        {
            ParametrosReduzirDe reduzirDe => ReduzirDeUmaModalidade(codigos, quantidades, excesso, reduzirDe),
            ParametrosReduzirProporcionalEm proporcional => ReduzirProporcionalmente(codigos, quantidades, excesso, proporcional),
            _ => throw new UnreachableException(
                $"A regra de ajuste do quadro {parametros.GetType().Name} não é uma das variantes de {nameof(ParametrosRegraAjusteDistribuicao)}."),
        };

    private static Result<QuadroReconciliado> ReduzirDeUmaModalidade(
        IReadOnlyList<string> codigos, IReadOnlyList<int> quantidades, int excesso, ParametrosReduzirDe parametros)
    {
        int indice = IndiceDe(codigos, parametros.ModalidadeCodigo);
        if (indice < 0)
        {
            return ModalidadeForaDoQuadro();
        }

        return quantidades[indice] < excesso
            ? ExcessoNaoAbsorvido("A modalidade do ajuste não tem vagas suficientes para absorver o excesso.")
            : Retirar(quantidades, [indice], [excesso], excesso);
    }

    private static Result<QuadroReconciliado> ReduzirProporcionalmente(
        IReadOnlyList<string> codigos, IReadOnlyList<int> quantidades, int excesso, ParametrosReduzirProporcionalEm parametros)
    {
        if (parametros.ModalidadeCodigos.Any(codigo => IndiceDe(codigos, codigo) < 0))
        {
            return ModalidadeForaDoQuadro();
        }

        int[] alvos = [.. parametros.ModalidadeCodigos.Select(codigo => IndiceDe(codigos, codigo))];
        int disponivel = alvos.Sum(indice => quantidades[indice]);
        return disponivel < excesso
            ? ExcessoNaoAbsorvido("As modalidades do ajuste somam menos vagas do que o excesso a absorver.")
            : Retirar(quantidades, alvos, CortesProporcionais(quantidades, alvos, excesso, disponivel), excesso);
    }

    // Maior resto: a parte inteira de cada proporção deixa sobra, e distribuí-la pelos maiores
    // restos é o que faz o total retirado bater exatamente com o excesso. Sem isso o quadro
    // fecharia com uma ou duas vagas de diferença, conforme o arredondamento.
    private static int[] CortesProporcionais(IReadOnlyList<int> quantidades, int[] alvos, int excesso, int disponivel)
    {
        long[] numeradores = [.. alvos.Select(indice => (long)quantidades[indice] * excesso)];
        int sobra = excesso - (int)numeradores.Sum(numerador => numerador / disponivel);
        HashSet<int> comResto = [.. Enumerable.Range(0, alvos.Length)
            .OrderByDescending(posicao => numeradores[posicao] % disponivel)
            .ThenBy(posicao => alvos[posicao])
            .Take(sobra)];

        return [.. numeradores.Select((numerador, posicao) => (int)(numerador / disponivel) + (comResto.Contains(posicao) ? 1 : 0))];
    }

    private static Result<QuadroReconciliado> Retirar(
        IReadOnlyList<int> quantidades, int[] indices, int[] cortes, int excesso)
    {
        int[] reduzidas = [.. quantidades];
        for (int i = 0; i < indices.Length; i++)
        {
            reduzidas[indices[i]] -= cortes[i];
        }

        return Result<QuadroReconciliado>.Success(new QuadroReconciliado(reduzidas, excesso));
    }

    private static Result<QuadroReconciliado> ExcessoNaoAbsorvido(string detalhe) =>
        Result<QuadroReconciliado>.Failure(new DomainError("ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso", detalhe));

    private static int IndiceDe(IReadOnlyList<string> codigos, string codigo)
    {
        for (int i = 0; i < codigos.Count; i++)
        {
            if (string.Equals(codigos[i], codigo, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static Result<QuadroReconciliado> ModalidadeForaDoQuadro() =>
        Result<QuadroReconciliado>.Failure(new DomainError(
            "ConfiguracaoDistribuicaoVagas.AjusteReferenciaModalidadeForaDoQuadro",
            "O ajuste reduz de modalidade que não está entre as modalidades selecionadas."));
}
