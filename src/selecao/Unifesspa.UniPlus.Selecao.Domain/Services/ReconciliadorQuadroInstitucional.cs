namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Diagnostics;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Reduz um quadro fixado pelo edital cuja soma passa do <c>VO_base</c>, aplicando o motor
/// que a regra de ajuste declara. Serviço de domínio puro: recebe o quadro já resolvido e
/// não consulta cadastro (ADR-0013).
/// </summary>
public static class ReconciliadorQuadroInstitucional
{
    /// <param name="Quantidades">Quadro após a redução, na mesma ordem de entrada.</param>
    /// <param name="Reduzido">Total retirado — o que o contrato publica como estouro absorvido.</param>
    public sealed record QuadroReconciliado(IReadOnlyList<int> Quantidades, int Reduzido);

    /// <summary>
    /// Retira <paramref name="excesso"/> vagas do quadro, distribuindo conforme o motor.
    /// </summary>
    /// <remarks>
    /// Recusa quando as modalidades que o motor nomeia não existem no quadro ou não somam o
    /// excesso: reduzir o que se pode e devolver um quadro que continua estourado seria
    /// entregar como reconciliado o que não fecha.
    /// </remarks>
    public static Result<QuadroReconciliado> Reduzir(
        IReadOnlyList<string> codigos,
        IReadOnlyList<int> quantidades,
        int excesso,
        ArgsRegraAjusteDistribuicao args)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(quantidades);
        ArgumentNullException.ThrowIfNull(args);

        if (excesso <= 0)
        {
            return Result<QuadroReconciliado>.Success(new QuadroReconciliado(quantidades, Reduzido: 0));
        }

        return args switch
        {
            ArgsReduzirDe reduzirDe => ReduzirDeUmaModalidade(codigos, quantidades, excesso, reduzirDe),
            ArgsReduzirProporcionalEm proporcional => ReduzirProporcionalmente(codigos, quantidades, excesso, proporcional),
            _ => throw new UnreachableException(
                $"O motor {args.GetType().Name} não é uma das variantes de {nameof(ArgsRegraAjusteDistribuicao)}."),
        };
    }

    private static Result<QuadroReconciliado> ReduzirDeUmaModalidade(
        IReadOnlyList<string> codigos,
        IReadOnlyList<int> quantidades,
        int excesso,
        ArgsReduzirDe args)
    {
        int indice = IndiceDe(codigos, args.ModalidadeCodigo);
        if (indice < 0)
        {
            return ModalidadeForaDoQuadro(args.ModalidadeCodigo);
        }

        if (quantidades[indice] < excesso)
        {
            return Result<QuadroReconciliado>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso",
                $"A modalidade {args.ModalidadeCodigo} tem {quantidades[indice]} vaga(s) e o excesso é de {excesso}."));
        }

        int[] reduzidas = [.. quantidades];
        reduzidas[indice] -= excesso;

        return Result<QuadroReconciliado>.Success(new QuadroReconciliado(reduzidas, excesso));
    }

    private static Result<QuadroReconciliado> ReduzirProporcionalmente(
        IReadOnlyList<string> codigos,
        IReadOnlyList<int> quantidades,
        int excesso,
        ArgsReduzirProporcionalEm args)
    {
        List<int> alvos = [];
        foreach (string codigo in args.ModalidadeCodigos)
        {
            int indice = IndiceDe(codigos, codigo);
            if (indice < 0)
            {
                return ModalidadeForaDoQuadro(codigo);
            }

            alvos.Add(indice);
        }

        int disponivel = alvos.Sum(i => quantidades[i]);
        if (disponivel < excesso)
        {
            return Result<QuadroReconciliado>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso",
                $"As modalidades do ajuste somam {disponivel} vaga(s) e o excesso é de {excesso}."));
        }

        int[] reduzidas = [.. quantidades];

        // Maior resto: a parte inteira de cada proporção deixa sobra, e distribuí-la pelos
        // maiores restos é o que faz o total retirado bater exatamente com o excesso. Sem
        // isso o quadro fecharia com uma ou duas vagas de diferença, conforme o arredondamento.
        int[] cortePorAlvo = new int[alvos.Count];
        long[] restos = new long[alvos.Count];
        int distribuido = 0;

        for (int i = 0; i < alvos.Count; i++)
        {
            long numerador = (long)quantidades[alvos[i]] * excesso;
            cortePorAlvo[i] = (int)(numerador / disponivel);
            restos[i] = numerador % disponivel;
            distribuido += cortePorAlvo[i];
        }

        foreach (int i in Enumerable.Range(0, alvos.Count)
            .OrderByDescending(i => restos[i])
            .ThenBy(i => alvos[i])
            .Take(excesso - distribuido))
        {
            cortePorAlvo[i]++;
        }

        for (int i = 0; i < alvos.Count; i++)
        {
            reduzidas[alvos[i]] -= cortePorAlvo[i];
        }

        return Result<QuadroReconciliado>.Success(new QuadroReconciliado(reduzidas, excesso));
    }

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

    private static Result<QuadroReconciliado> ModalidadeForaDoQuadro(string codigo) =>
        Result<QuadroReconciliado>.Failure(new DomainError(
            "ConfiguracaoDistribuicaoVagas.AjusteReferenciaModalidadeForaDoQuadro",
            $"O ajuste reduz de {codigo}, que não está entre as modalidades selecionadas."));
}
