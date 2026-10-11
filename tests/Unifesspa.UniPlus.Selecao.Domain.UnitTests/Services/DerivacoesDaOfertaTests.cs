namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A modalidade do candidato e a opção pelas cotas da lei saem das modalidades ofertadas (ADR-0144):
/// a modalidade é a união das regras congeladas, e a opção só existe com cota da lei ofertada.
/// </summary>
public sealed class DerivacoesDaOfertaTests
{
    private static readonly (string, bool) Optou = (OpcaoPelasCotasDaLei.Fato, true);
    private static readonly (string, bool) NaoOptou = (OpcaoPelasCotasDaLei.Fato, false);

    private static FatoResolvido Sim => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true));

    private static FatoResolvido Nao => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false));

    [Fact(DisplayName = "Sem modalidade ofertada, a oferta não deriva nada")]
    public void SemOferta_NaoDeriva() =>
        DerivacoesDaOferta.Efetivas([], Array.Empty<ModalidadeSelecionada>()).Should().BeEmpty();

    [Fact(DisplayName = "Com cota da lei ofertada, a opção pelas cotas é derivada da escola pública e da opção pelas vagas de escola pública")]
    public void ComCota_DerivaAOpcao()
    {
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas = DerivacoesDaOferta.Efetivas([], [Oferta(
            Modalidade("AC", NaturezaLegalModalidade.Ampla),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, [Optou]))]);

        ConfiguracaoDerivacaoFato opcao = efetivas.Should().ContainSingle(d => d.CodigoFato == OpcaoPelasCotasDaLei.Fato).Which;
        opcao.Booleano.Should().BeTrue();
        opcao.FatosCitados.Should().BeEquivalentTo(OpcaoPelasCotasDaLei.DependenciasDaOpcao);
    }

    [Fact(DisplayName = "Com cota da lei ofertada e sem o egresso configurado, o egresso vem pela regra do sistema")]
    public void ComCota_SemEgressoConfigurado_UsaARegraDoSistema()
    {
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas = DerivacoesDaOferta.Efetivas([], [Oferta(
            Modalidade("AC", NaturezaLegalModalidade.Ampla),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, [Optou]))]);

        ConfiguracaoDerivacaoFato egresso = efetivas.Should().ContainSingle(d => d.CodigoFato == OrigemEscolar.FatoEgresso).Which;
        egresso.Booleano.Should().BeTrue();
        egresso.FatosCitados.Should().BeEquivalentTo(OrigemEscolar.DependenciasDoEgresso);
    }

    [Fact(DisplayName = "O egresso que o processo configura prevalece sobre a regra do sistema")]
    public void EgressoConfigurado_Prevalece()
    {
        ConfiguracaoDerivacaoFato configurado = ConfiguracaoDerivacaoFato.Criar(
            OrigemEscolar.FatoEgresso, [RegraDerivacaoConfigurada.Criar(0, null, null).Value!]).Value!;

        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas = DerivacoesDaOferta.Efetivas([configurado], [Oferta(
            Modalidade("AC", NaturezaLegalModalidade.Ampla),
            Modalidade("LI_EP", NaturezaLegalModalidade.CotaReservada, [Optou]))]);

        efetivas.Should().ContainSingle(d => d.CodigoFato == OrigemEscolar.FatoEgresso).Which.Should().BeSameAs(configurado);
    }

    [Fact(DisplayName = "Sem cota da lei ofertada, o egresso não vem pela regra do sistema")]
    public void SemCota_NaoSupreOEgresso() =>
        DerivacoesDaOferta.Efetivas([], [Oferta(
                Modalidade("AC", NaturezaLegalModalidade.Ampla),
                Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, [NaoOptou, ("PCD", true)]))])
            .Should().NotContain(d => d.CodigoFato == OrigemEscolar.FatoEgresso);

    [Fact(DisplayName = "A modalidade do candidato é a união das regras das modalidades ofertadas, uma por código")]
    public void Modalidade_UneAsRegrasOfertadas()
    {
        ModalidadeSelecionada pcd = Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, [NaoOptou, ("PCD", true)]);
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas = DerivacoesDaOferta.Efetivas([], [
            Oferta(Modalidade("AC", NaturezaLegalModalidade.Ampla), pcd, Modalidade("LI_PCD", NaturezaLegalModalidade.CotaReservada, [Optou, ("PCD", true)])),
            Oferta(Modalidade("AC", NaturezaLegalModalidade.Ampla), pcd, Modalidade("LI_PCD", NaturezaLegalModalidade.CotaReservada, [Optou, ("PCD", true)])),
        ]);

        string[] modalidades = Derivar(efetivas, (OpcaoPelasCotasDaLei.Fato, Nao), ("PCD", Sim));

        modalidades.Should().Equal("AC", "AC_PCD");
        efetivas.Single(d => d.CodigoFato == ModalidadeDoCandidato.Fato).Regras.Should().HaveCount(3, "a modalidade em duas ofertas tem uma regra só");
    }

    [Fact(DisplayName = "Sem cota da lei ofertada, a opção pelas cotas vale falso: não é derivada e sai das regras")]
    public void SemCota_OpcaoValeFalso()
    {
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas = DerivacoesDaOferta.Efetivas([], [Oferta(
            Modalidade("AC", NaturezaLegalModalidade.Ampla),
            Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, [NaoOptou, ("PCD", true), ("CONCORRER_PCD", true)]),
            Modalidade("ESPECIAL", NaturezaLegalModalidade.AcaoAfirmativa, [Optou, ("PCD", true)]))]);

        efetivas.Should().NotContain(d => d.CodigoFato == OpcaoPelasCotasDaLei.Fato);
        ConfiguracaoDerivacaoFato modalidade = efetivas.Single(d => d.CodigoFato == ModalidadeDoCandidato.Fato);
        modalidade.FatosCitados.Should().BeEquivalentTo(["PCD", "CONCORRER_PCD"]);
        modalidade.Regras.Select(static r => r.Contribui).Should().Equal(["AC", "AC_PCD"],
            "a modalidade que só vale para quem optou não tem candidato sem cota da lei ofertada");
        Derivar(efetivas, ("PCD", Sim), ("CONCORRER_PCD", Sim)).Should().Equal("AC", "AC_PCD");
    }

    [Fact(DisplayName = "Sem cota da lei ofertada, a cláusula que só recusava a opção vale para todos")]
    public void SemCota_ClausulaSoComARecusaVale() =>
        Derivar(
                DerivacoesDaOferta.Efetivas([], [Oferta(
                    Modalidade("AC", NaturezaLegalModalidade.Ampla),
                    Modalidade("AC_X", NaturezaLegalModalidade.AcaoAfirmativa, [NaoOptou]))]))
            .Should().Equal("AC", "AC_X");

    [Fact(DisplayName = "As derivações que o processo configura continuam entre as efetivas")]
    public void Configuradas_Continuam()
    {
        ConfiguracaoDerivacaoFato configurada = ConfiguracaoDerivacaoFato.Criar(
            "PRETO_DECLARADO", [RegraDerivacaoConfigurada.Criar(0, null, null).Value!]).Value!;

        DerivacoesDaOferta.Efetivas([configurada], [Oferta(Modalidade("AC", NaturezaLegalModalidade.Ampla))])
            .Select(static d => d.CodigoFato).Should().Equal("PRETO_DECLARADO", ModalidadeDoCandidato.Fato);
    }

    private static string[] Derivar(IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas, params (string Fato, FatoResolvido Valor)[] fatos)
    {
        ConfiguracaoDerivacaoFato modalidade = efetivas.Single(d => d.CodigoFato == ModalidadeDoCandidato.Fato);
        string[] codigos = [.. modalidade.Regras.Select(static r => r.Contribui!)];
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(
            modalidade.ParaRegrasDerivacao(codigos).Value!,
            fatos.ToDictionary(static f => f.Fato, static f => f.Valor, StringComparer.Ordinal));
        resultado.Estado.Should().Be(EstadoFato.Resolvido);
        return [.. resultado.Valores.Order(StringComparer.Ordinal)];
    }

    private static ConfiguracaoDistribuicaoVagas Oferta(params ModalidadeSelecionada[] modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 10 * modalidades.Length, pr: 1m,
            ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null, referenciaDemografica: null, modalidades, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static ModalidadeSelecionada Modalidade(
        string codigo, NaturezaLegalModalidade natureza, IReadOnlyList<(string Fato, bool Valor)>? atomos = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, ComposicaoVagasModalidade.SuplementarAoTotal, null,
            natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null,
            atomos is null
                ? PredicadoDnf.SemClausula
                : PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
                    a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!,
            null, "base legal", quantidadeDeclarada: 10).Value!;
}
