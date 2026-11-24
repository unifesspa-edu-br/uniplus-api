namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Montagem comum aos testes de <see cref="Modalidade"/>: a factory com defaults válidos e as regras de aplicabilidade mínimas.</summary>
internal static class ApoioDaModalidade
{
    /// <summary>
    /// A declaração válida (ampla, restante das vagas); o teste informa só o que difere dela, com <c>ajuste</c>.
    /// Sem regra de aplicabilidade declarada, vale a mínima que a natureza admite.
    /// </summary>
    internal static DeclaracaoDaModalidade Declarar(Func<DeclaracaoDaModalidade, DeclaracaoDaModalidade>? ajuste = null)
    {
        DeclaracaoDaModalidade declarada = ajuste?.Invoke(DeclaracaoPadrao) ?? DeclaracaoPadrao;

        return declarada with { RegraDeAplicabilidade = declarada.RegraDeAplicabilidade ?? AplicavelA(declarada.CodigoDaNaturezaLegal) };
    }

    internal static Result<Modalidade> Criar(
        string codigo = "AC", Func<DeclaracaoDaModalidade, DeclaracaoDaModalidade>? ajuste = null) =>
        Modalidade.Criar(codigo, Declarar(ajuste));

    /// <summary>Cota da lei: reservada, dentro das vagas reservadas, cascata.</summary>
    internal static DeclaracaoDaModalidade ComoCotaReservada(this DeclaracaoDaModalidade declaracao) => declaracao with
    {
        CodigoDaNaturezaLegal = "COTA_RESERVADA",
        CodigoDaComposicaoDeVagas = "DENTRO_DA_RESERVA_LEGAL",
        CodigoDaRegraDeRemanejamento = "SEGUE_ORDEM_DE_REMANEJAMENTO",
    };

    /// <summary>Ação afirmativa suplementar ao total, com a regra de remanejamento dada.</summary>
    internal static DeclaracaoDaModalidade ComoAcaoAfirmativa(this DeclaracaoDaModalidade declaracao, string regraRemanejamento) => declaracao with
    {
        CodigoDaNaturezaLegal = "ACAO_AFIRMATIVA",
        CodigoDaComposicaoDeVagas = "SUPLEMENTAR_AO_TOTAL",
        CodigoDaRegraDeRemanejamento = regraRemanejamento,
    };

    private static DeclaracaoDaModalidade DeclaracaoPadrao { get; } = new(
        null, "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS", null, null, null, null, null, null, null, null);

    /// <summary>A regra de aplicabilidade mínima que a natureza admite.</summary>
    internal static PredicadoDnf AplicavelA(string? natureza) => natureza switch
    {
        "COTA_RESERVADA" => ComOpcaoPelasCotas(optou: true),
        "ACAO_AFIRMATIVA" => ComOpcaoPelasCotas(optou: false),
        _ => Incondicional(),
    };

    internal static PredicadoDnf Incondicional() => PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!;

    internal static PredicadoDnf ComOpcaoPelasCotas(bool optou, params (string Fato, object Valor)[] demais) =>
        Clausulas([[(OpcaoPelasCotasDaLei.Fato, optou), .. demais]]);

    internal static PredicadoDnf Clausulas(params (string Fato, object Valor)[][] clausulas) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. clausulas.SelectMany((atomos, indice) => atomos.Select(a => (indice,
                CondicaoDnf.Criar(a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!)))]).Value!;

    /// <summary>Cota federal tal como semeada: reservada, dentro das vagas reservadas, cascata.</summary>
    internal static Modalidade CotaFederal(PredicadoDnf? aplicabilidade = null) =>
        Criar("LB_PPI", d => d.ComoCotaReservada() with
        {
            Descricao = "Cota",
            RegraDeAplicabilidade = aplicabilidade,
            BaseLegal = "Lei 12.711/2012",
        }).Value!;

    /// <summary>A regra da pessoa com deficiência na ampla concorrência: ser pessoa com deficiência e optar por concorrer como tal, sem citar a opção pelas cotas.</summary>
    internal static PredicadoDnf RegraDaAcPcd() => Clausulas([("PCD", true), ("CONCORRER_PCD", true)]);

    /// <summary>Pessoa com deficiência na ampla concorrência: única fixa com parâmetro de remanejamento.</summary>
    internal static Modalidade AcPcd() =>
        Criar("AC_PCD", d => d with
        {
            RegraDeAplicabilidade = RegraDaAcPcd(),
            CodigoDaNaturezaLegal = "ACAO_AFIRMATIVA",
            CodigoDaComposicaoDeVagas = "RETIRADA_DE_OUTRA_MODALIDADE",
            ComposicaoOrigem = "AC",
            CodigoDaRegraDeRemanejamento = "DESTINO_UNICO",
            RemanejamentoDestino = "AC",
        }).Value!;
}
