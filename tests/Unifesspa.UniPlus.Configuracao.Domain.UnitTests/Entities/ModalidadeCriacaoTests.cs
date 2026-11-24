namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

/// <summary>Criação de <see cref="Modalidade"/>: campos básicos, defaults, código e limites de tamanho.</summary>
public sealed class ModalidadeCriacaoTests
{
    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Ampla sem remanejamento preenche os campos e fica ativa com Guid v7")]
    public void Criar_AmplaSemRemanejamento_Valida()
    {
        Modalidade m = Criar("AC", d => d with { Descricao = "Ampla concorrência", CodigoDaNaturezaLegal = "AMPLA" }).ValorEsperado();

        m.Id.Should().NotBe(Guid.Empty);
        m.Codigo.Valor.Should().Be("AC");
        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.ComposicaoVagas.Should().Be(ComposicaoVagas.RestanteDasVagasOfertadas);
        m.RegraRemanejamento.Should().BeNull();
        m.RemanejamentoParametros.TemAlgum.Should().BeFalse();
        m.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Cota reservada com SEGUE_ORDEM_DE_REMANEJAMENTO é válida")]
    public void Criar_CotaComCascata_Valida()
    {
        Modalidade m = Criar("LB_PPI", d => d.ComoCotaReservada()).Value!;

        m.NaturezaLegal.Should().Be(NaturezaLegal.CotaReservada);
        m.RegraRemanejamento.Should().Be(RegraRemanejamento.SegueOrdemDeRemanejamento);
    }

    [Fact(DisplayName = "Natureza e composição ausentes assumem defaults AMPLA / RESTANTE_DAS_VAGAS_OFERTADAS")]
    public void Criar_SemNaturezaComposicao_AplicaDefaults()
    {
        Modalidade m = Criar(ajuste: d => d with { CodigoDaNaturezaLegal = null, CodigoDaComposicaoDeVagas = null }).ValorEsperado();

        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.ComposicaoVagas.Should().Be(ComposicaoVagas.RestanteDasVagasOfertadas);
    }

    // ── Formato do código ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Código com hífen, minúscula ou espaço é rejeitado (formato)")]
    [InlineData("lb_ppi")]
    [InlineData("LB-PPI")]
    [InlineData("LB PPI")]
    public void Criar_CodigoForaDoFormato_Falha(string codigo)
    {
        Result<Modalidade> r = Criar(codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<Modalidade> r = Criar(codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoObrigatorio);
    }

    // ── Ação quando indeferido (invariante 6) ──────────────────────────────────

    [Fact(DisplayName = "Ação quando indeferido fora do domínio é rejeitada")]
    public void Criar_AcaoInvalida_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with { CodigoDaAcaoQuandoIndeferido = "REPROVAR" });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.AcaoIndeferimentoInvalida);
    }

    [Fact(DisplayName = "Ação quando indeferido válida é aceita")]
    public void Criar_AcaoValida_Aceita()
    {
        Modalidade m = Criar(ajuste: d => d with { CodigoDaAcaoQuandoIndeferido = "RECLASSIFICAR_AC" }).ValorEsperado();

        m.AcaoQuandoIndeferido.Should().Be(AcaoQuandoIndeferido.ReclassificarAc);
    }

    // ── Descrição e base legal ─────────────────────────────────────────────────

    [Fact(DisplayName = "Descrição acima de 300 caracteres é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with { Descricao = new string('a', 301) });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Base legal acima de 500 caracteres é rejeitada")]
    public void Criar_BaseLegalLonga_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with { BaseLegal = new string('a', 501) });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.BaseLegalTamanho);
    }

    [Fact(DisplayName = "A factory materializa códigos legais fixos — a reserva é do cadastro, não do domínio")]
    public void Criar_CodigoLegalFixo_Aceita()
    {
        Result<Modalidade> r = Criar("LB_PPI", d => d.ComoCotaReservada());

        r.IsSuccess.Should().BeTrue("o seed e a reidratação dependem da factory aceitá-los");
    }
}
