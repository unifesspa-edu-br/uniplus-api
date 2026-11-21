namespace Unifesspa.UniPlus.Regras.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A opção pelas cotas da Lei nº 12.711/2012: ser egresso de escola pública e optar por concorrer às
/// vagas de escola pública (UNI-REQ-0076). Toda cota da lei a exige e toda ação afirmativa a recusa,
/// porque o candidato não concorre às duas ao mesmo tempo (UNI-REQ-0142). A única exceção é a
/// ação afirmativa de pessoa com deficiência (<see cref="AcaoAfirmativaQueSeSomaAsCotas"/>), que não
/// cita a opção e vale para quem optou pelas cotas e para quem não optou (UNI-REQ-0266).
/// </summary>
public static class OpcaoPelasCotasDaLei
{
    /// <summary>O fato derivado da opção pelas cotas da lei.</summary>
    public const string Fato = "OPTOU_PELAS_COTAS_DA_LEI";

    /// <summary>A opção por concorrer às vagas reservadas a egressos de escola pública.</summary>
    public const string FatoConcorrerEscolaPublica = "CONCORRER_EP";

    /// <summary>
    /// O código da reserva de vagas para pessoa com deficiência, a única ação afirmativa a que quem
    /// optou pelas cotas da lei também concorre: a Resolução Unifesspa 532/2021 (art. 1º) a prevê
    /// em qualquer processo, com ou sem cotas (UNI-REQ-0142 e UNI-REQ-0266).
    /// </summary>
    public const string AcaoAfirmativaQueSeSomaAsCotas = "AC_PCD";

    /// <summary>A regra padrão: egresso de escola pública que optou pelas vagas de escola pública.</summary>
    public static IReadOnlyList<RegraDerivacao> RegrasDaOpcao { get; } =
    [
        RegraDerivacao.CriarBooleana(Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (1, Condicao(OrigemEscolar.FatoEgresso, true)),
            (1, Condicao(FatoConcorrerEscolaPublica, true)),
        ]))),
    ];

    /// <summary>Os fatos de que a opção pelas cotas depende: os que a regra dela cita.</summary>
    public static IReadOnlyList<string> DependenciasDaOpcao { get; } =
        [.. RegrasDaOpcao.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Se a cláusula exige a opção pelas cotas com o valor dado: verdadeiro na cota da lei, falso na
    /// ação afirmativa.
    /// </summary>
    public static bool ClausulaExige(ClausulaDnf clausula, bool optou)
    {
        ArgumentNullException.ThrowIfNull(clausula);
        return clausula.Condicoes.Any(c =>
            string.Equals(c.Fato, Fato, StringComparison.Ordinal)
            && c.Operador == Operador.Igual
            && c.Valor.ValueKind == (optou ? JsonValueKind.True : JsonValueKind.False));
    }

    /// <summary>
    /// Se toda cláusula da regra exige a opção pelas cotas com o valor dado: é a forma da regra de uma
    /// cota da lei (verdadeiro) e de uma ação afirmativa (falso). A regra sem cláusula não exige nada.
    /// </summary>
    public static bool TodaClausulaExige(PredicadoDnf regra, bool optou)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return regra.Clausulas.Count > 0 && regra.Clausulas.All(c => ClausulaExige(c, optou));
    }

    /// <summary>Se a ação afirmativa tem o código da que se soma às cotas da lei, e não de outra.</summary>
    public static bool SeSomaAsCotas(string codigoDaAcaoAfirmativa) =>
        string.Equals(codigoDaAcaoAfirmativa, AcaoAfirmativaQueSeSomaAsCotas, StringComparison.Ordinal);

    /// <summary>
    /// Se a regra tem a forma que a ação afirmativa exige. A que se soma às cotas tem condição e não
    /// cita a opção pelas cotas; as demais exigem, em toda cláusula, que o candidato não tenha optado.
    /// </summary>
    public static bool RegraTemAFormaDaAcaoAfirmativa(string codigoDaAcaoAfirmativa, PredicadoDnf regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return SeSomaAsCotas(codigoDaAcaoAfirmativa)
            ? CitaCondicaoSemACitarAOpcao(regra)
            : TodaClausulaExige(regra, optou: false);
    }

    private static bool CitaCondicaoSemACitarAOpcao(PredicadoDnf regra)
    {
        IReadOnlyCollection<string> citados = regra.FatosCitados;
        return citados.Count > 0 && !citados.Contains(Fato, StringComparer.Ordinal);
    }

    private static CondicaoDnf Condicao(string fato, bool valor) =>
        Exigir(CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)));

    /// <summary>A regra é fixa no código: o que o domínio recusasse aqui é defeito, não recusa ao usuário.</summary>
    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"A regra da opção pelas cotas da lei é recusada pelo domínio: {resultado.Error!.Message}");
}
