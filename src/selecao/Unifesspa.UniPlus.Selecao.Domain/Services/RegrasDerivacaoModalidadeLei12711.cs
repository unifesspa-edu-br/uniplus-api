namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Constrói a regra de derivação de <c>MODALIDADE</c> do ramo Lei 12.711/2012 (red. Lei 14.723/2023)
/// — a matriz R0–R9 (Story #927). É a configuração normativa desse ramo, reutilizável pelo cadastro e
/// pelos testes; um processo do ramo institucional traz a sua própria.
/// </summary>
/// <remarks>
/// <para>
/// R0 é a âncora incondicional: todo candidato concorre à ampla concorrência (concorrência dupla da
/// Lei 14.723/2023). As demais contribuem uma cota quando o candidato optou por concorrer a ela e é
/// elegível — a elegibilidade e o gate de escola pública são garantidos pela pré-condição do opt-in
/// na coleta, mas cada regra ainda exige explicitamente <c>EGRESSO_ESCOLA_PUBLICA</c> das subcotas,
/// para que o motor recuse entrada inconsistente sem depender do grafo de coleta.
/// </para>
/// <para>
/// <c>LB ⊂ LI</c> é estrutural: cada regra <c>LB_*</c> repete os átomos da <c>LI_*</c> irmã mais o
/// átomo de renda. A modalidade de pessoa com deficiência fora da reserva federal é <c>AC_PCD</c> —
/// nunca o rótulo <c>V</c> — e independe do gate de escola pública (exceção do corpus).
/// </para>
/// </remarks>
public static class RegrasDerivacaoModalidadeLei12711
{
    public const string CodigoFato = "MODALIDADE";

    private const string ConcorrerPcd = "CONCORRER_PCD";
    private const string ConcorrerEp = "CONCORRER_EP";
    private const string ConcorrerPpi = "CONCORRER_PPI";
    private const string ConcorrerQ = "CONCORRER_Q";
    private const string ConcorrerRenda = "CONCORRER_RENDA";
    private const string EgressoEscolaPublica = "EGRESSO_ESCOLA_PUBLICA";

    /// <summary>O domínio canônico de MODALIDADE no ramo Lei 12.711.</summary>
    public static IReadOnlyCollection<string> DominioCanonico { get; } =
        ["AC", "AC_PCD", "LI_EP", "LB_EP", "LI_PPI", "LB_PPI", "LI_Q", "LB_Q", "LI_PCD", "LB_PCD"];

    /// <summary>Constrói a matriz R0–R9 como regra de derivação de MODALIDADE.</summary>
    public static RegrasDerivacaoFato Construir()
    {
        List<RegraDerivacao> regras =
        [
            Ancora("AC"),
            Regra("AC_PCD", (ConcorrerPcd, true)),
            Regra("LI_PCD", (ConcorrerPcd, true), (EgressoEscolaPublica, true)),
            Regra("LB_PCD", (ConcorrerPcd, true), (EgressoEscolaPublica, true), (ConcorrerRenda, true)),
            Regra("LI_EP", (EgressoEscolaPublica, true), (ConcorrerEp, true)),
            Regra("LB_EP", (EgressoEscolaPublica, true), (ConcorrerEp, true), (ConcorrerRenda, true)),
            Regra("LI_PPI", (EgressoEscolaPublica, true), (ConcorrerPpi, true)),
            Regra("LB_PPI", (EgressoEscolaPublica, true), (ConcorrerPpi, true), (ConcorrerRenda, true)),
            Regra("LI_Q", (EgressoEscolaPublica, true), (ConcorrerQ, true)),
            Regra("LB_Q", (EgressoEscolaPublica, true), (ConcorrerQ, true), (ConcorrerRenda, true)),
        ];

        string[] dependencias =
            [ConcorrerPcd, EgressoEscolaPublica, ConcorrerEp, ConcorrerPpi, ConcorrerQ, ConcorrerRenda];

        return RegrasDerivacaoFato.Criar(CodigoFato, regras, dependencias, DominioCanonico).Value!;
    }

    private static RegraDerivacao Ancora(string contribui) =>
        RegraDerivacao.Criar(PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, contribui).Value!;

    private static RegraDerivacao Regra(string contribui, params (string Fato, bool Valor)[] atomos)
    {
        // Todos os átomos de uma regra vivem na MESMA cláusula (E lógico) — a ordinal 1.
        var linhas = atomos
            .Select(a => (Clausula: 1, Condicao: CondicaoDnf.Criar(
                a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))
            .ToList();

        return RegraDerivacao.Criar(PredicadoDnf.CriarDeCondicoesAgrupadas(linhas).Value!, contribui).Value!;
    }
}
