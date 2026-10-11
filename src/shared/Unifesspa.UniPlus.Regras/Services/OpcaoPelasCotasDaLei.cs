namespace Unifesspa.UniPlus.Regras.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A opção pelas cotas da Lei nº 12.711/2012: ser egresso de escola pública e optar por concorrer às
/// vagas de escola pública (UNI-REQ-0076). Toda cota da lei a exige e toda ação afirmativa a recusa,
/// porque o candidato nunca concorre às duas ao mesmo tempo (UNI-REQ-0142).
/// </summary>
public static class OpcaoPelasCotasDaLei
{
    /// <summary>O fato derivado da opção pelas cotas da lei.</summary>
    public const string Fato = "OPTOU_PELAS_COTAS_DA_LEI";

    /// <summary>A opção por concorrer às vagas reservadas a egressos de escola pública.</summary>
    public const string FatoConcorrerEscolaPublica = "CONCORRER_EP";

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

    private static CondicaoDnf Condicao(string fato, bool valor) =>
        Exigir(CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)));

    /// <summary>A regra é fixa no código: o que o domínio recusasse aqui é defeito, não recusa ao usuário.</summary>
    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"A regra da opção pelas cotas da lei é recusada pelo domínio: {resultado.Error!.Message}");
}
