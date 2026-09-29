namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Constrói a regra de derivação de <c>MODALIDADE</c> do ramo Lei 12.711/2012 (red. Lei 14.723/2023)
/// — a matriz R0–R9 (Story #927, UNI-REQ-0076). É a configuração normativa desse ramo, reutilizável
/// pelo cadastro e pelos testes; um processo do ramo institucional traz a sua própria.
/// </summary>
/// <remarks>
/// <para>
/// R0 é a âncora incondicional: todo candidato concorre à ampla concorrência (concorrência dupla da
/// Lei 14.723/2023). Optar pelas cotas da lei é ser egresso de escola pública e optar por concorrer
/// às vagas de escola pública, e toda cota exige essa opção — cada regra repete os dois átomos, para
/// que o motor recuse entrada inconsistente sem depender do grafo de coleta. <c>LB ⊂ LI</c> é
/// estrutural: cada regra <c>LB_*</c> repete os átomos da <c>LI_*</c> irmã mais o átomo de renda.
/// </para>
/// <para>
/// <c>AC_PCD</c> é ação afirmativa (UNI-REQ-0141) e não convive com cota da lei (UNI-REQ-0142): vale
/// para a pessoa com deficiência que não optou pelas cotas — por não ser egressa de escola pública ou
/// por tê-las recusado. As duas cláusulas dela contradizem, átomo a átomo, as das cotas.
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
        (string, bool) optouPelasCotas1 = (EgressoEscolaPublica, true);
        (string, bool) optouPelasCotas2 = (ConcorrerEp, true);

        List<RegraDerivacao> regras =
        [
            Ancora("AC"),
            Regra("AC_PCD",
                [(ConcorrerPcd, true), (EgressoEscolaPublica, false)],
                [(ConcorrerPcd, true), (ConcorrerEp, false)]),
            Regra("LI_PCD", [optouPelasCotas1, optouPelasCotas2, (ConcorrerPcd, true)]),
            Regra("LB_PCD", [optouPelasCotas1, optouPelasCotas2, (ConcorrerPcd, true), (ConcorrerRenda, true)]),
            Regra("LI_EP", [optouPelasCotas1, optouPelasCotas2]),
            Regra("LB_EP", [optouPelasCotas1, optouPelasCotas2, (ConcorrerRenda, true)]),
            Regra("LI_PPI", [optouPelasCotas1, optouPelasCotas2, (ConcorrerPpi, true)]),
            Regra("LB_PPI", [optouPelasCotas1, optouPelasCotas2, (ConcorrerPpi, true), (ConcorrerRenda, true)]),
            Regra("LI_Q", [optouPelasCotas1, optouPelasCotas2, (ConcorrerQ, true)]),
            Regra("LB_Q", [optouPelasCotas1, optouPelasCotas2, (ConcorrerQ, true), (ConcorrerRenda, true)]),
        ];

        string[] dependencias =
            [ConcorrerPcd, EgressoEscolaPublica, ConcorrerEp, ConcorrerPpi, ConcorrerQ, ConcorrerRenda];

        return RegrasDerivacaoFato.Criar(CodigoFato, regras, dependencias, DominioCanonico).Value!;
    }

    /// <summary>
    /// A proposta para o processo que não oferta nenhuma cota da lei — ampla concorrência e a reserva
    /// de pessoa com deficiência. Escola pública e opção pelas cotas não são coletadas ali, então
    /// <c>AC_PCD</c> depende só do opt-in por deficiência (UNI-REQ-0076).
    /// </summary>
    public static RegrasDerivacaoFato ConstruirSemCotasDaLei()
    {
        List<RegraDerivacao> regras =
        [
            Ancora("AC"),
            Regra("AC_PCD", [(ConcorrerPcd, true)]),
        ];

        return RegrasDerivacaoFato.Criar(CodigoFato, regras, [ConcorrerPcd], ["AC", "AC_PCD"]).Value!;
    }

    private static RegraDerivacao Ancora(string contribui) =>
        RegraDerivacao.Criar(PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, contribui).Value!;

    /// <summary>Cada lista de átomos é uma cláusula (E lógico); as cláusulas se somam em OU.</summary>
    private static RegraDerivacao Regra(string contribui, params (string Fato, bool Valor)[][] clausulas)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [.. clausulas
            .SelectMany((atomos, indice) => atomos.Select(a => (Clausula: indice + 1, Condicao: CondicaoDnf.Criar(
                a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!)))];

        return RegraDerivacao.Criar(PredicadoDnf.CriarDeCondicoesAgrupadas(linhas).Value!, contribui).Value!;
    }
}
