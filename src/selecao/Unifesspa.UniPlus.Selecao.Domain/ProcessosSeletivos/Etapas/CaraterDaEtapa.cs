namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O caráter da etapa: se ela pontua, se ela elimina, o que o edital pode declarar de peso e de nota
/// mínima conforme ele, e o que o tipo de etapa admite. É o que as regras da etapa consultam em vez de
/// repetir quais valores do enum pontuam ou eliminam.
/// </summary>
public static class CaraterDaEtapa
{
    /// <summary>A etapa pontua quando o caráter é classificatório ou ambos.</summary>
    public static bool Pontua(CaraterEtapa carater) => carater is CaraterEtapa.Classificatoria or CaraterEtapa.Ambas;

    /// <summary>A etapa elimina quando o caráter é eliminatório ou ambos.</summary>
    public static bool Elimina(CaraterEtapa carater) => carater is CaraterEtapa.Eliminatoria or CaraterEtapa.Ambas;

    /// <summary>
    /// Acumula as recusas do caráter declarado e do que ele permite declarar (ADR-0125), nesta ordem: o
    /// caráter em si, o peso e a nota mínima. A recusa de peso e de nota mínima por caráter só vale para
    /// o caráter definido: quem não declarou caráter recebe só a recusa do caráter.
    /// </summary>
    public static List<FieldError> ValidarDeclarado(CaraterEtapa carater, decimal? peso, decimal? notaMinima)
    {
        FieldError?[] recusas =
        [
            RecusaDoCarater(carater), RecusaDoPeso(carater, peso), RecusaDaNotaMinima(carater, notaMinima),
            PrecisaoDecimal.Recusa(peso, "peso", PrecisaoDecimal.Peso),
            PrecisaoDecimal.Recusa(notaMinima, "notaMinima", PrecisaoDecimal.NotaMinima),
        ];

        return [.. recusas.OfType<FieldError>()];
    }

    /// <summary>
    /// Confere o caráter declarado contra o que o tipo de etapa admite, acumulando as duas violações
    /// possíveis (ADR-0125).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fora de <see cref="ValidarDeclarado"/> de propósito: aquela checagem é de forma, e esta depende de
    /// onde vêm os sinalizadores — do <see cref="ValueObjects.TipoEtapaSnapshot"/> da etapa, que é o que a
    /// declaração da etapa confere, ou do cadastro vivo quando a gravação lê o tipo (etapa nova, vínculo
    /// novo ou caráter alterado). Quem chama escolhe a fonte (ADR-0129).
    /// </para>
    /// <para>
    /// A reidratação de uma versão congelada não confere: reidratar não é declarar, e o par caráter e
    /// sinalizadores volta como foi conferido ao congelar.
    /// </para>
    /// </remarks>
    public static List<FieldError> ValidarAdmitido(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, string nomeDoTipo)
    {
        FieldError?[] recusas =
        [
            Pontua(carater) && !admitePontuacao
                ? NaoAdmitido($"O tipo de etapa '{nomeDoTipo}' não compõe a nota final, então a etapa não pode ter caráter que pontua.")
                : null,
            Elimina(carater) && !admiteEliminacao
                ? NaoAdmitido($"O tipo de etapa '{nomeDoTipo}' não elimina candidato, então a etapa não pode ter caráter que reprova.")
                : null,
        ];

        return [.. recusas.OfType<FieldError>()];
    }

    private static FieldError NaoAdmitido(string mensagem) =>
        new("carater", new DomainError(EtapaProcessoErrorCodes.CaraterNaoAdmitidoPeloTipo, mensagem));

    private static FieldError? RecusaDoCarater(CaraterEtapa carater) =>
        carater == CaraterEtapa.Nenhum || !Enum.IsDefined(carater)
            ? new("carater", new DomainError(
                "EtapaProcesso.CaraterObrigatorio",
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas)."))
            : null;

    // Peso em etapa que não pontua é dado morto: o divisor da média soma apenas as etapas cujo caráter
    // compõe a nota, então o valor fica gravado sem nunca pesar em nada, e quem configurou acredita que pesa.
    private static FieldError? RecusaDoPeso(CaraterEtapa carater, decimal? peso)
    {
        if (peso is <= 0)
        {
            return new("peso", new DomainError(
                "EtapaProcesso.PesoInvalido", "O peso da etapa, quando informado, deve ser maior que zero."));
        }

        return peso.HasValue && carater is CaraterEtapa.Eliminatoria
            ? new("peso", new DomainError(
                "EtapaProcesso.PesoSemCaraterQuePontua",
                "Peso só se aplica a etapa cujo caráter compõe a nota final; etapa apenas eliminatória aprova ou reprova sem pontuar."))
            : null;
    }

    private static FieldError? RecusaDaNotaMinima(CaraterEtapa carater, decimal? notaMinima)
    {
        if (notaMinima is < 0)
        {
            return new("notaMinima", new DomainError(
                "EtapaProcesso.NotaMinimaInvalida", "A nota mínima, quando informada, não pode ser negativa."));
        }

        return notaMinima.HasValue && carater is CaraterEtapa.Classificatoria
            ? new("notaMinima", new DomainError(
                "EtapaProcesso.NotaMinimaSemCaraterQueElimina",
                "Nota mínima só se aplica a etapa cujo caráter elimina; etapa apenas classificatória pontua sem reprovar."))
            : null;
    }
}
