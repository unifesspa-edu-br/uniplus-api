namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ModalidadeTests
{
    private static Result<Modalidade> Criar(
        string codigo = "AC",
        string? descricao = null,
        string? natureza = "AMPLA",
        string? composicao = "RESIDUAL_DO_VO",
        string? origem = null,
        string? regra = null,
        string? destino = null,
        string? par = null,
        string? fallback = null,
        IReadOnlyList<string>? criterios = null,
        string? acao = null,
        string? baseLegal = null) =>
        Modalidade.Criar(
            codigo, descricao, natureza, composicao, origem, regra,
            destino, par, fallback, criterios, acao, baseLegal);

    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Ampla sem remanejamento preenche os campos e fica ativa com Guid v7")]
    public void Criar_AmplaSemRemanejamento_Valida()
    {
        Modalidade m = Criar(
            codigo: "AC", descricao: "Ampla concorrência", natureza: "AMPLA").Value!;

        m.Id.Should().NotBe(Guid.Empty);
        m.Codigo.Valor.Should().Be("AC");
        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.ComposicaoVagas.Should().Be(ComposicaoVagas.ResidualDoVo);
        m.RegraRemanejamento.Should().BeNull();
        m.RemanejamentoArgs.TemAlgum.Should().BeFalse();
        m.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Cota reservada com SEGUE_CASCATA é válida")]
    public void Criar_CotaComCascata_Valida()
    {
        Modalidade m = Criar(
            codigo: "LB_PPI", natureza: "COTA_RESERVADA", composicao: "DENTRO_DO_VR",
            regra: "SEGUE_CASCATA").Value!;

        m.NaturezaLegal.Should().Be(NaturezaLegal.CotaReservada);
        m.RegraRemanejamento.Should().Be(RegraRemanejamento.SegueCascata);
    }

    [Fact(DisplayName = "Natureza e composição ausentes assumem defaults AMPLA / RESIDUAL_DO_VO")]
    public void Criar_SemNaturezaComposicao_AplicaDefaults()
    {
        Modalidade m = Criar(natureza: null, composicao: null).Value!;

        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.ComposicaoVagas.Should().Be(ComposicaoVagas.ResidualDoVo);
    }

    // ── Formato do código ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Código com hífen, minúscula ou espaço é rejeitado (formato)")]
    [InlineData("lb_ppi")]
    [InlineData("LB-PPI")]
    [InlineData("LB PPI")]
    public void Criar_CodigoForaDoFormato_Falha(string codigo)
    {
        Result<Modalidade> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<Modalidade> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoObrigatorio);
    }

    // ── Coerência natureza ↔ remanejamento (invariante 3) ──────────────────────

    [Fact(DisplayName = "Cota reservada SEM cascata é incoerente")]
    public void Criar_CotaSemCascata_Incoerente()
    {
        Result<Modalidade> r = Criar(natureza: "COTA_RESERVADA", composicao: "DENTRO_DO_VR", regra: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Ampla COM remanejamento é incoerente")]
    public void Criar_AmplaComRemanejamento_Incoerente()
    {
        Result<Modalidade> r = Criar(natureza: "AMPLA", regra: "SEGUE_CASCATA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Suplementar com SEGUE_CASCATA é incoerente (exige destino único ou cruzado)")]
    public void Criar_SuplementarComCascata_Incoerente()
    {
        Result<Modalidade> r = Criar(
            natureza: "SUPLEMENTAR", composicao: "SUPLEMENTAR_AO_TOTAL", regra: "SEGUE_CASCATA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Contraprova: Suplementar com DESTINO_UNICO + destino é coerente")]
    public void Criar_SuplementarComDestinoUnico_Coerente()
    {
        Result<Modalidade> r = Criar(
            natureza: "SUPLEMENTAR", composicao: "SUPLEMENTAR_AO_TOTAL",
            regra: "DESTINO_UNICO", destino: "AC");

        r.IsSuccess.Should().BeTrue();
        r.Value!.RegraRemanejamento.Should().Be(RegraRemanejamento.DestinoUnico);
    }

    // ── Equivalência composição RETIRA_DE ⟺ origem (invariante 4) ──────────────

    [Fact(DisplayName = "RETIRA_DE sem origem é rejeitado (origem obrigatória)")]
    public void Criar_RetiraDeSemOrigem_Falha()
    {
        Result<Modalidade> r = Criar(composicao: "RETIRA_DE", origem: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.OrigemObrigatoriaParaRetiraDe);
    }

    [Fact(DisplayName = "Origem preenchida sem RETIRA_DE é rejeitada (origem apenas para RETIRA_DE)")]
    public void Criar_OrigemSemRetiraDe_Falha()
    {
        Result<Modalidade> r = Criar(composicao: "RESIDUAL_DO_VO", origem: "AC");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.OrigemApenasParaRetiraDe);
    }

    [Fact(DisplayName = "Contraprova: RETIRA_DE com origem é aceito")]
    public void Criar_RetiraDeComOrigem_Aceita()
    {
        Modalidade m = Criar(natureza: "AMPLA", composicao: "RETIRA_DE", origem: "AC").Value!;

        m.ComposicaoVagas.Should().Be(ComposicaoVagas.RetiraDe);
        m.ComposicaoOrigem.Should().Be("AC");
    }

    // ── Argumentos por regra (invariante 5) ────────────────────────────────────

    [Fact(DisplayName = "DESTINO_UNICO sem destino é rejeitado")]
    public void Criar_DestinoUnicoSemDestino_Falha()
    {
        Result<Modalidade> r = Criar(
            natureza: "SUPLEMENTAR", composicao: "SUPLEMENTAR_AO_TOTAL", regra: "DESTINO_UNICO");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio);
    }

    [Fact(DisplayName = "CRUZADO sem par/fallback é rejeitado")]
    public void Criar_CruzadoSemParFallback_Falha()
    {
        Result<Modalidade> r = Criar(
            natureza: "OUTRA_MODALIDADE", composicao: "SUPLEMENTAR_AO_TOTAL",
            regra: "CRUZADO", par: "AC");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio);
    }

    [Fact(DisplayName = "Contraprova: CRUZADO com par e fallback é aceito")]
    public void Criar_CruzadoComParFallback_Aceita()
    {
        Modalidade m = Criar(
            natureza: "OUTRA_MODALIDADE", composicao: "SUPLEMENTAR_AO_TOTAL",
            regra: "CRUZADO", par: "AC", fallback: "LB_PPI").Value!;

        m.RemanejamentoArgs.Par.Should().Be("AC");
        m.RemanejamentoArgs.Fallback.Should().Be("LB_PPI");
        m.RemanejamentoArgs.Destino.Should().BeNull();
    }

    [Fact(DisplayName = "SEGUE_CASCATA com argumentos preenchidos é rejeitado (nenhum arg admitido)")]
    public void Criar_CascataComArgs_Falha()
    {
        Result<Modalidade> r = Criar(
            natureza: "COTA_RESERVADA", composicao: "DENTRO_DO_VR",
            regra: "SEGUE_CASCATA", destino: "AC");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio);
    }

    // ── Ação quando indeferido (invariante 6) ──────────────────────────────────

    [Fact(DisplayName = "Ação quando indeferido fora do domínio é rejeitada")]
    public void Criar_AcaoInvalida_Falha()
    {
        Result<Modalidade> r = Criar(acao: "REPROVAR");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.AcaoIndeferimentoInvalida);
    }

    [Fact(DisplayName = "Ação quando indeferido válida é aceita")]
    public void Criar_AcaoValida_Aceita()
    {
        Modalidade m = Criar(acao: "RECLASSIFICAR_AC").Value!;

        m.AcaoQuandoIndeferido.Should().Be(AcaoQuandoIndeferido.ReclassificarAc);
    }

    // ── Descrição e base legal ─────────────────────────────────────────────────

    [Fact(DisplayName = "Descrição acima de 300 caracteres é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<Modalidade> r = Criar(descricao: new string('a', 301));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Base legal acima de 500 caracteres é rejeitada")]
    public void Criar_BaseLegalLonga_Falha()
    {
        Result<Modalidade> r = Criar(baseLegal: new string('a', 501));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.BaseLegalTamanho);
    }

    // ── Imutabilidade do código na atualização ─────────────────────────────────

    [Fact(DisplayName = "Atualizar troca atributos editáveis mantendo Codigo e Id imutáveis")]
    public void Atualizar_MantemCodigoEId()
    {
        Modalidade m = Criar(codigo: "LB_PPI", natureza: "COTA_RESERVADA", regra: "SEGUE_CASCATA").Value!;
        Guid idOriginal = m.Id;

        Result r = m.Atualizar(
            descricao: "Nova descrição", naturezaLegal: "AMPLA", composicaoVagas: "RESIDUAL_DO_VO",
            composicaoOrigem: null, regraRemanejamento: null,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsSuccess.Should().BeTrue();
        m.Codigo.Valor.Should().Be("LB_PPI", "o código é imutável");
        m.Id.Should().Be(idOriginal, "o Id é imutável");
        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.Descricao.Should().Be("Nova descrição");
    }

    [Fact(DisplayName = "Atualizar revalida coerência (cota sem cascata falha)")]
    public void Atualizar_Incoerente_Falha()
    {
        Modalidade m = Criar(codigo: "AC", natureza: "AMPLA").Value!;

        Result r = m.Atualizar(
            descricao: null, naturezaLegal: "COTA_RESERVADA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: null,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }
}
