namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As regras de aplicabilidade congeladas na publicação de um processo com inscrição própria: cada
/// uma cita só o que a inscrição pergunta ou o processo deriva, lida como vale na oferta, e tem a
/// forma que a natureza da modalidade pede no cadastro.
/// </summary>
public sealed class RegrasDasModalidadesNaPublicacaoTests
{
    private const string ItemFatoNaoColetado = "modalidade_regra_cita_fato_nao_coletado";
    private const string ItemNatureza = "modalidade_regra_incoerente_com_a_natureza";

    [Fact(DisplayName = "Sem cota da lei ofertada, a reserva PcD que recusa a opção pelas cotas publica com as perguntas de PcD na inscrição")]
    public void SemCota_ReservaPcd_Publica()
    {
        ProcessoSeletivo processo = ProcessoComAmplaEReservaPcd();
        processo.DefinirItens([Booleano("PCD", 0), Booleano("CONCORRER_PCD", 1)]).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull(
            "sem cota da lei ofertada, ninguém optou pelas cotas, e a regra cita só o que a inscrição pergunta");
    }

    [Fact(DisplayName = "A regra que cita fato coletado só na isenção é recusada: a modalidade é decidida pela inscrição")]
    public void FatoSoNaIsencao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComAmplaEReservaPcd();
        processo.DefinirItens([Booleano("PCD", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Booleano("CONCORRER_PCD", 0)], finalidade: FinalidadeFormulario.IsencaoTaxa).IsSuccess.Should().BeTrue();

        Item(processo, ItemFatoNaoColetado).Ok.Should().BeFalse("CONCORRER_PCD é perguntado só na isenção");
    }

    [Fact(DisplayName = "A regra que cita fato coletado na inscrição é aceita")]
    public void FatoNaInscricao_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComAmplaEReservaPcd();
        processo.DefinirItens([Booleano("PCD", 0), Booleano("CONCORRER_PCD", 1)]).IsSuccess.Should().BeTrue();

        Item(processo, ItemFatoNaoColetado).Ok.Should().BeTrue();
    }

    [Theory(DisplayName = "A regra que cita a opção pelas cotas depende da opção pelas vagas de escola pública perguntada na inscrição")]
    [InlineData(FinalidadeFormulario.Inscricao, true)]
    [InlineData(FinalidadeFormulario.Habilitacao, false)]
    public void OpcaoPelasCotas_DependeDaInscricao(FinalidadeFormulario finalidade, bool esperadoOk)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, Exige((OpcaoPelasCotasDaLei.Fato, true))));
        processo.DefinirRegrasDerivacao(
            [ConfiguracaoDerivacaoFato.Criar(OrigemEscolar.FatoEgresso, [RegraDerivacaoConfigurada.Criar(0, null, null).Value!]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Booleano(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 0)], finalidade: finalidade).IsSuccess.Should().BeTrue();

        Item(processo, ItemFatoNaoColetado).Ok.Should().Be(esperadoOk,
            "a opção pelas cotas é derivada da opção pelas vagas de escola pública, que a inscrição tem de perguntar");
    }

    [Fact(DisplayName = "A cota da lei copiada sem a opção pelas cotas — a linha anterior à cópia da regra — recusa a publicação")]
    public void CotaSemOpcao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo, Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula), Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, PredicadoDnf.SemClausula));

        Item(processo, ItemNatureza).Ok.Should().BeFalse();
        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code
            .Should().Be(ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza);
    }

    [Theory(DisplayName = "A regra congelada tem a forma que a natureza pede: a cota exige a opção, e a ação afirmativa a recusa")]
    [InlineData(NaturezaLegalModalidade.CotaReservada, true, true)]
    [InlineData(NaturezaLegalModalidade.CotaReservada, false, false)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, false, true)]
    [InlineData(NaturezaLegalModalidade.AcaoAfirmativa, true, false)]
    public void Natureza_DecideAForma(NaturezaLegalModalidade natureza, bool optou, bool esperadoOk)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(processo, Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula), Modalidade("X", natureza, Exige((OpcaoPelasCotasDaLei.Fato, optou))));

        Item(processo, ItemNatureza).Ok.Should().Be(esperadoOk);
    }

    /// <summary>A ampla e a reserva PcD de ampla concorrência, sem cota da lei, no processo conforme com inscrição própria.</summary>
    private static ProcessoSeletivo ProcessoComAmplaEReservaPcd()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Ofertar(
            processo,
            Modalidade("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Modalidade(ModalidadesFederaisLei12711.AcPcd, NaturezaLegalModalidade.AcaoAfirmativa,
                Exige((OpcaoPelasCotasDaLei.Fato, false), ("PCD", true), ("CONCORRER_PCD", true))));
        return processo;
    }

    private static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo).Single(i => i.Codigo == codigo);

    private static void Ofertar(ProcessoSeletivo processo, params ModalidadeSelecionada[] modalidades) =>
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 10 * modalidades.Length, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
                null, null, modalidades, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static ModalidadeSelecionada Modalidade(string codigo, NaturezaLegalModalidade natureza, PredicadoDnf regra) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.ResidualDoVo : ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null, regra, null, "base legal", quantidadeDeclarada: 10).Value!;

    private static FatoColetado Booleano(string fato, int ordem) =>
        FatoColetado.Criar(fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "SENSIVEL").Value!;

    private static PredicadoDnf Exige(params (string Fato, bool Valor)[] atomos) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
            a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!;
}
