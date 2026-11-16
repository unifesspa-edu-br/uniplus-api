namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>As distribuições declaradas que os testes das conferências de vagas partem de válidas.</summary>
internal static class DistribuicaoDeVagasDeTeste
{
    internal static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.De(codigo, "v1", new string('a', 64));

    internal static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    internal static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        ComposicaoVagasModalidade composicao,
        int? quantidadeDeclarada = null,
        string? composicaoOrigemCodigo = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, composicao, composicaoOrigemCodigo,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), null, "base legal", quantidadeDeclarada).Value!;

    internal static ModalidadeSelecionada Federal(string codigo) =>
        Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal);

    internal static ModalidadeSelecionada AmplaConcorrencia() =>
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas);

    internal static ModalidadeSelecionada Fixada(string codigo, int quantidade) =>
        Modalidade(codigo, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, quantidade);

    internal static List<ModalidadeSelecionada> AsOitoFederaisMaisAmplaConcorrencia() =>
        [.. ModalidadesFederaisLei12711.Codigos.Select(Federal), AmplaConcorrencia()];

    internal static DistribuicaoDeVagasDeclarada DaLeiDeCotas() => new()
    {
        RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Lei12711),
        VagasOfertadasBase = 50,
        PercentualDaReservaLegal = 0.5m,
        ReferenciaDemografica = Demografica(),
        RegraDeAjuste = Regra("RECONCILIACAO-VAGAS-ART11-PU"),
        Modalidades = AsOitoFederaisMaisAmplaConcorrencia(),
    };

    internal static DistribuicaoDeVagasDeclarada FixadaNoEdital(params ModalidadeSelecionada[] modalidades) => new()
    {
        RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional),
        VagasOfertadasBase = 50,
        Modalidades = modalidades,
    };
}
