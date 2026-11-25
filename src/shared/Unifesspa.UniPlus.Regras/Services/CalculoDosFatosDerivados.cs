namespace Unifesspa.UniPlus.Regras.Services;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Deriva o valor de um fato a partir da sua regra de derivação e dos fatos resolvidos do candidato.
/// Função pura: avalia todas as regras ativas e une as contribuições.
/// </summary>
/// <remarks>
/// <para>
/// A união é de conjunto — idempotente e comutativa. Não há conflito por construção: duas regras que
/// contribuem o mesmo código o incluem uma vez, e a ordem de avaliação não importa. Nenhuma regra
/// ativa produz o conjunto vazio, que é um resultado <b>resolvido</b> — "concorre a nenhuma cota" é
/// uma resposta, não uma pendência.
/// </para>
/// <para>
/// O gate de dependências vem <b>antes</b> da avaliação das regras, e prevalece sobre o colapso local
/// de uma cláusula: se qualquer dependente declarado está ausente ou indeterminado, o derivado é
/// indeterminado como um todo (fail-closed), sem tentar unir contribuições parciais. Um dependente
/// não-aplicável ou não informado (opcional em branco com a etapa concluída), por outro lado, é
/// informação resolvida — não bloqueia; a regra que o cita simplesmente não contribui (a cláusula
/// colapsa falso).
/// </para>
/// </remarks>
public static class CalculoDosFatosDerivados
{
    /// <summary>
    /// Versão semântica do interpretador de derivação, congelada no envelope de publicação (congelamento dos parâmetros na publicação, UNI-REQ-0019).
    /// É a identidade da semântica com que um snapshot publicado foi resolvido — muda quando a
    /// forma como as regras são avaliadas muda (não a cada deploy), para que um snapshot antigo
    /// continue reidratável com a semântica que o produziu. A "2" acrescenta o derivado booleano:
    /// verdadeiro se alguma regra ativa, falso se nenhuma (ADR-0136).
    /// </summary>
    public const string VersaoSemantica = "2";

    public static ResultadoDerivacao Derivar(
        DerivacaoDoFato derivacao,
        IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(derivacao);
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        return derivacao.DependenciasDeclaradas.Any(dependencia =>
            !fatosResolvidos.TryGetValue(dependencia, out FatoResolvido? fato)
            || fato is null || fato.Estado == EstadoFato.Indeterminado)
            ? ResultadoDerivacao.Indeterminado
            : AvaliarRegras(derivacao, fatosResolvidos);
    }

    // Um valor de tipo incoerente ainda pode tornar a avaliação indeterminada, mesmo após o gate.
    private static ResultadoDerivacao AvaliarRegras(DerivacaoDoFato derivacao, IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        HashSet<string> derivado = new(StringComparer.Ordinal);
        bool algumaAtiva = false;
        foreach (RegraDerivacao regra in derivacao.Regras)
        {
            Ternario avaliacao = regra.AvaliarQuando(fatosResolvidos);
            if (avaliacao == Ternario.Indeterminado)
            {
                return ResultadoDerivacao.Indeterminado;
            }
            AcrescentarContribuicao(regra, avaliacao, derivado, ref algumaAtiva);
        }
        return derivacao.Booleano ? ResultadoDerivacao.ResolvidoBooleano(algumaAtiva) : ResultadoDerivacao.Resolvido(derivado);
    }

    private static void AcrescentarContribuicao(RegraDerivacao regra, Ternario avaliacao, HashSet<string> derivado, ref bool algumaAtiva)
    {
        if (avaliacao == Ternario.Verdadeiro)
        {
            algumaAtiva = true;
            if (regra.Contribui is { } contribui)
            {
                derivado.Add(contribui);
            }
        }
    }
}
