namespace Unifesspa.UniPlus.Regras.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A origem escolar do candidato em duas perguntas — como concluiu o ensino médio e onde o cursou —
/// e o egresso de escola pública que o sistema deriva delas para as cotas da Lei nº 12.711/2012
/// (UNI-REQ-0148).
/// </summary>
/// <remarks>
/// É egresso quem cursou o ensino médio somente em escola pública, em escola comunitária do campo
/// conveniada ou parte em uma e parte na outra, qualquer que seja a forma de conclusão, e quem
/// concluiu por certificação — ENCCEJA, exame de proficiência ou ENEM — sem ter frequentado o ensino
/// médio. Escola privada, com ou sem bolsa, Sistema S e exterior não contam como rede pública
/// (Portaria Normativa MEC nº 18/2012, art. 5º, § 1º), nem em parte do ensino médio.
/// </remarks>
public static class OrigemEscolar
{
    /// <summary>Como o candidato concluiu o ensino médio.</summary>
    public const string FatoFormaDeConclusao = "FORMA_CONCLUSAO_EM";

    /// <summary>Onde o candidato cursou o ensino médio.</summary>
    public const string FatoOndeCursou = "ONDE_CURSOU_EM";

    /// <summary>O egresso de escola pública, derivado das duas respostas.</summary>
    public const string FatoEgresso = "EGRESSO_ESCOLA_PUBLICA";

    public const string Regular = "REGULAR";
    public const string Eja = "EJA";
    public const string Encceja = "ENCCEJA";
    public const string Proficiencia = "PROFICIENCIA";
    public const string Enem = "ENEM";

    public const string SomentePublica = "SOMENTE_PUBLICA";
    public const string ComunitariaDoCampo = "COMUNITARIA_CAMPO";
    public const string PrivadaComBolsaIntegral = "PRIVADA_BOLSA_INTEGRAL";
    public const string PrivadaComBolsaParcial = "PRIVADA_BOLSA_PARCIAL";
    public const string Privada = "PRIVADA";
    public const string PublicaEPrivadaComBolsaIntegral = "PUBLICA_E_PRIVADA_BOLSA_INTEGRAL";
    public const string PublicaEForaDaRede = "PUBLICA_E_FORA_DA_REDE";
    public const string PublicaEComunitaria = "PUBLICA_E_COMUNITARIA";
    public const string SistemaS = "SISTEMA_S";
    public const string Exterior = "EXTERIOR";
    public const string MaisDeUmForaDaRede = "MAIS_DE_UM_FORA_DA_REDE";
    public const string ConcluiuPorCertificacao = "CERTIFICACAO";

    /// <summary>Onde cursou, nas opções que contam como rede pública para as cotas.</summary>
    private static readonly string[] RedePublica = [SomentePublica, ComunitariaDoCampo, PublicaEComunitaria];

    /// <summary>
    /// As formas de conclusão por certificação: só a elas se oferece "Concluí por certificação", e todas
    /// contam para as cotas a quem não frequentou o ensino médio.
    /// </summary>
    public static IReadOnlyList<string> Certificacoes { get; } = [Encceja, Proficiencia, Enem];

    /// <summary>Onde cursou, nas opções que se oferecem a todo candidato.</summary>
    public static IReadOnlyList<string> OndeCursouSemCertificacao { get; } =
    [
        SomentePublica, ComunitariaDoCampo, PrivadaComBolsaIntegral, PrivadaComBolsaParcial, Privada,
        PublicaEPrivadaComBolsaIntegral, PublicaEForaDaRede, PublicaEComunitaria, SistemaS, Exterior, MaisDeUmForaDaRede,
    ];

    /// <summary>A regra do egresso de escola pública: verdadeiro se alguma cláusula vale, falso se nenhuma.</summary>
    public static IReadOnlyList<RegraDerivacao> RegrasDoEgresso { get; } =
    [
        RegraDerivacao.CriarBooleana(Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (1, Condicao(FatoOndeCursou, Operador.Em, RedePublica)),
            (2, Condicao(FatoOndeCursou, Operador.Igual, ConcluiuPorCertificacao)),
            (2, Condicao(FatoFormaDeConclusao, Operador.Em, Certificacoes)),
        ]))),
    ];

    /// <summary>Os fatos de que o egresso de escola pública depende: os que a regra dele cita.</summary>
    public static IReadOnlyList<string> DependenciasDoEgresso { get; } =
        [.. RegrasDoEgresso.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>A derivação do egresso de escola pública.</summary>
    public static RegrasDerivacaoFato DerivacaoDoEgresso() =>
        Exigir(RegrasDerivacaoFato.CriarBooleana(FatoEgresso, RegrasDoEgresso, DependenciasDoEgresso));

    private static CondicaoDnf Condicao<T>(string fato, Operador operador, T valor) =>
        Exigir(CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)));

    /// <summary>A regra é fixa no código: o que o domínio recusasse aqui é defeito, não recusa ao usuário.</summary>
    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"A regra do egresso de escola pública é recusada pelo domínio: {resultado.Error!.Message}");
}
