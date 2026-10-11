namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using Atomo = (string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, object Valor);

/// <summary>
/// As regras de aplicabilidade das modalidades do catálogo legal fixo (ADR-0144, UNI-REQ-0076,
/// UNI-REQ-0096): quem concorre a cada uma, pelos fatos do candidato. Cada regra é o critério da
/// própria modalidade, sem citar outra.
/// </summary>
/// <remarks>
/// <para>
/// A ampla concorrência é incondicional: todo candidato concorre a ela (Lei nº 12.711/2012, art. 3º,
/// § 2º). Cada cota da lei exige a opção pelas cotas e, na dimensão dela, a elegibilidade e a opção
/// por concorrer. Na cota de pretos, pardos e indígenas, quem se declara indígena pela cor só se
/// enquadra com o pertencimento indígena declarado. A cota de renda repete cada cláusula da cota
/// independente de renda e acrescenta a renda:
/// quem se enquadra nela também concorre à outra, que é reservada independentemente da renda
/// (Portaria Normativa MEC nº 18/2012, art. 10, IV).
/// </para>
/// <para>
/// As ações afirmativas exigem não ter optado pelas cotas da lei, porque o candidato nunca concorre
/// às duas (UNI-REQ-0142): a reserva de pessoa com deficiência pede só ser pessoa com deficiência e
/// optar por ela, e as vagas por acréscimo pedem a autodeclaração indígena ou quilombola e a opção
/// por elas, sem condição de escola, renda ou cor.
/// </para>
/// </remarks>
internal static class RegrasDeAplicabilidadeLegais
{
    private static readonly Atomo OptouPelasCotas = (OpcaoPelasCotasDaLei.Fato, Operador.Igual, true);
    private static readonly Atomo NaoOptouPelasCotas = (OpcaoPelasCotasDaLei.Fato, Operador.Igual, false);
    private static readonly Atomo Renda = ("BAIXA_RENDA", Operador.Igual, true);
    private static readonly Atomo ConcorrerRenda = ("CONCORRER_RENDA", Operador.Igual, true);
    private static readonly Atomo PretaOuParda = ("COR_RACA", Operador.Em, new[] { "PRETA", "PARDA" });
    private static readonly Atomo IndigenaPelaCor = ("COR_RACA", Operador.Igual, "INDIGENA");
    private static readonly Atomo ConcorrerPpi = ("CONCORRER_PPI", Operador.Igual, true);
    private static readonly Atomo Quilombola = ("QUILOMBOLA", Operador.Igual, true);
    private static readonly Atomo ConcorrerQ = ("CONCORRER_Q", Operador.Igual, true);
    private static readonly Atomo Pcd = ("PCD", Operador.Igual, true);
    private static readonly Atomo ConcorrerPcd = ("CONCORRER_PCD", Operador.Igual, true);
    private static readonly Atomo Indigena = ("INDIGENA", Operador.Igual, true);
    private static readonly Atomo ConcorrerVagaPorAcrescimo = ("CONCORRER_VAGA_POR_ACRESCIMO", Operador.Igual, true);

    private static readonly Atomo[] Ep = [OptouPelasCotas];
    private static readonly Atomo[][] Ppi =
    [
        [OptouPelasCotas, PretaOuParda, ConcorrerPpi],
        [OptouPelasCotas, IndigenaPelaCor, Indigena, ConcorrerPpi],
    ];

    private static readonly Atomo[] Q = [OptouPelasCotas, Quilombola, ConcorrerQ];
    private static readonly Atomo[] CotaPcd = [OptouPelasCotas, Pcd, ConcorrerPcd];

    public static PredicadoDnf AmplaConcorrencia { get; } = Regra();

    public static PredicadoDnf LiEp { get; } = Regra(Ep);

    public static PredicadoDnf LbEp { get; } = Regra(ComRenda(Ep));

    public static PredicadoDnf LiPpi { get; } = Regra(Ppi);

    public static PredicadoDnf LbPpi { get; } = Regra([.. Ppi.Select(ComRenda)]);

    public static PredicadoDnf LiQ { get; } = Regra(Q);

    public static PredicadoDnf LbQ { get; } = Regra(ComRenda(Q));

    public static PredicadoDnf LiPcd { get; } = Regra(CotaPcd);

    public static PredicadoDnf LbPcd { get; } = Regra(ComRenda(CotaPcd));

    public static PredicadoDnf AcPcd { get; } = Regra([NaoOptouPelasCotas, Pcd, ConcorrerPcd]);

    public static PredicadoDnf AcI { get; } = Regra([NaoOptouPelasCotas, Indigena, ConcorrerVagaPorAcrescimo]);

    public static PredicadoDnf AcQ { get; } = Regra([NaoOptouPelasCotas, Quilombola, ConcorrerVagaPorAcrescimo]);

    private static Atomo[] ComRenda(Atomo[] clausula) => [.. clausula, Renda, ConcorrerRenda];

    /// <summary>O OU das cláusulas, cada uma o E dos seus átomos; sem cláusula, a regra incondicional.</summary>
    private static PredicadoDnf Regra(params Atomo[][] clausulas) =>
        Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. clausulas.SelectMany(static (atomos, indice) => atomos.Select(a =>
                (indice, Exigir(CondicaoDnf.Criar(a.Fato, a.Operador, JsonSerializer.SerializeToElement(a.Valor))))))]));

    /// <summary>A regra é fixa na semente: o que o domínio recusasse aqui é defeito, não recusa ao usuário.</summary>
    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"Regra de aplicabilidade semeada inválida: {resultado.Error!.Message}");
}
