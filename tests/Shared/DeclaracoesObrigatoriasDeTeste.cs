namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As declarações que o processo exige para publicar além da taxa: o bônus regional (aplica ou
/// não) e, com inscrição própria, ao menos um critério de desempate. Para fixtures que precisam
/// de um processo publicável sem que essas dimensões sejam o assunto.
/// </summary>
internal static class DeclaracoesObrigatoriasDeTeste
{
    /// <summary>
    /// Declara "não aplica bônus" e um critério de desempate, só onde falta: o que o teste já
    /// configurou fica como está. Os parâmetros deixam uma das duas por declarar, para provar a
    /// recusa correspondente.
    /// </summary>
    public static void Declarar(ProcessoSeletivo processo, bool bonus = true, bool desempate = true)
    {
        ArgumentNullException.ThrowIfNull(processo);

        if (bonus && processo.AplicaBonusRegional is null)
        {
            ExigirSucesso(processo.DefinirBonusRegional(aplica: false, bonus: null, PrecondicaoIfMatch.Ausente));
        }

        if (desempate && processo.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && processo.CriteriosDesempate.Count == 0)
        {
            ReferenciaRegra regra = ReferenciaRegra.Criar(CriterioDesempateCodigo.MaiorIdade, "v1", new string('f', 64)).Value!;
            CriterioDesempate criterio = CriterioDesempate.Criar(1, regra, new ArgsDesempateMaiorIdade()).Value!;
            ExigirSucesso(processo.DefinirCriteriosDesempate([criterio], PrecondicaoIfMatch.Ausente));
        }
    }

    private static void ExigirSucesso(Unifesspa.UniPlus.Kernel.Results.Result resultado)
    {
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException($"Fixture inválida: {resultado.Error?.Message}");
        }
    }
}
