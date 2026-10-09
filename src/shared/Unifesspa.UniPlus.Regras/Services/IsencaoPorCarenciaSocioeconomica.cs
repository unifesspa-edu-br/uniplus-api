namespace Unifesspa.UniPlus.Regras.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O direito à isenção da taxa de inscrição por carência socioeconômica, nos termos da Lei nº
/// 12.799/2013, art. 1º (UNI-REQ-0149): renda familiar per capita de até um salário mínimo e meio e
/// ensino médio completo cursado em escola da rede pública ou como bolsista integral da rede privada.
/// </summary>
/// <remarks>
/// A condição é sobre as respostas, sem fato derivado próprio: a renda é declarada no pedido de
/// isenção, e a origem escolar vem das duas perguntas da inscrição (UNI-REQ-0148). Não cursou o
/// ensino médio completo quem o concluiu por certificação — ENCCEJA, exame de proficiência ou ENEM —,
/// e a lei não cita a escola comunitária do campo conveniada; por isso o recorte difere do egresso de
/// escola pública das cotas, que conta as duas e não conta o bolsista integral.
/// </remarks>
public static class IsencaoPorCarenciaSocioeconomica
{
    /// <summary>A renda familiar per capita de até um salário mínimo e meio, inclusive, declarada no pedido.</summary>
    public const string FatoRenda = "RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO";

    /// <summary>As formas de conclusão de quem cursou o ensino médio: o EJA é curso, não certificação.</summary>
    public static IReadOnlyList<string> FormasDeConclusao { get; } = [OrigemEscolar.Regular, OrigemEscolar.Eja];

    /// <summary>Onde cursou, nas opções que a lei admite: rede pública ou bolsa integral na rede privada.</summary>
    public static IReadOnlyList<string> OndeCursou { get; } =
        [OrigemEscolar.SomentePublica, OrigemEscolar.PrivadaComBolsaIntegral, OrigemEscolar.PublicaEPrivadaComBolsaIntegral];

    /// <summary>A condição do direito: as três respostas ao mesmo tempo.</summary>
    public static PredicadoDnf Condicao { get; } = Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas(
    [
        (1, Exigir(CondicaoDnf.Criar(FatoRenda, Operador.Igual, JsonSerializer.SerializeToElement(true)))),
        (1, Exigir(CondicaoDnf.Criar(OrigemEscolar.FatoFormaDeConclusao, Operador.Em, JsonSerializer.SerializeToElement(FormasDeConclusao)))),
        (1, Exigir(CondicaoDnf.Criar(OrigemEscolar.FatoOndeCursou, Operador.Em, JsonSerializer.SerializeToElement(OndeCursou)))),
    ]));

    /// <summary>Os fatos da inscrição que a condição cita: o formulário de isenção os pressupõe, sem perguntá-los de novo.</summary>
    public static IReadOnlyList<string> FatosDaInscricao { get; } =
        [.. Condicao.FatosCitados.Where(static f => !string.Equals(f, FatoRenda, StringComparison.Ordinal))];

    /// <summary>A regra é fixa no código: o que o domínio recusasse aqui é defeito, não recusa ao usuário.</summary>
    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"A condição da isenção por carência socioeconômica é recusada pelo domínio: {resultado.Error!.Message}");
}
