namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Deriva o valor de um fato a partir da sua regra de derivação e dos fatos resolvidos do candidato
/// (Story #927). Função pura: avalia todas as regras ativas e une as contribuições.
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
/// não-aplicável, por outro lado, é informação resolvida — não bloqueia; a regra que o cita
/// simplesmente não contribui (a cláusula colapsa falso).
/// </para>
/// </remarks>
public static class MotorDerivacao
{
    /// <summary>
    /// Versão semântica do interpretador de derivação, congelada no envelope de publicação (RN08).
    /// É a identidade da semântica com que um snapshot publicado foi resolvido — muda quando a
    /// forma como o motor avalia as regras muda (não a cada deploy), para que um snapshot antigo
    /// continue reidratável com a semântica que o produziu. Enquanto há uma só semântica, é "1".
    /// </summary>
    public const string VersaoSemantica = "1";

    public static ResultadoDerivacao Derivar(
        RegrasDerivacaoFato regras,
        IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        foreach (string dependencia in regras.DependenciasDeclaradas)
        {
            if (!fatosResolvidos.TryGetValue(dependencia, out FatoResolvido? fato)
                || fato is null
                || fato.Estado == EstadoFato.Indeterminado)
            {
                return ResultadoDerivacao.Indeterminado;
            }
        }

        HashSet<string> derivado = new(StringComparer.Ordinal);
        foreach (RegraDerivacao regra in regras.Regras)
        {
            switch (regra.AvaliarQuando(fatosResolvidos))
            {
                case Ternario.Verdadeiro:
                    derivado.Add(regra.Contribui);
                    break;

                case Ternario.Indeterminado:
                    // O gate garante que nenhum dependente está indeterminado por estado, mas um
                    // fato resolvido com valor de tipo incoerente com o operador ainda avalia
                    // indeterminado. Fail-closed: não se decide o derivado sobre uma regra que o
                    // motor não sabe avaliar — trata-la como inativa esconderia o dado corrompido.
                    return ResultadoDerivacao.Indeterminado;

                case Ternario.Falso:
                default:
                    break;
            }
        }

        return ResultadoDerivacao.Resolvido(derivado);
    }
}
