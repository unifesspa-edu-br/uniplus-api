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

    [Fact(DisplayName = "CRUZADO sem par é rejeitado — o par é o que define o cruzamento")]
    public void Criar_CruzadoSemPar_Falha()
    {
        Result<Modalidade> r = Criar(
            natureza: "OUTRA_MODALIDADE", composicao: "SUPLEMENTAR_AO_TOTAL",
            regra: "CRUZADO", fallback: "AC");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio);
    }

    [Fact(DisplayName = "CRUZADO com par e sem fallback é aceito — o par sem destino final deixa a vaga ociosa")]
    public void Criar_CruzadoSemFallback_Aceita()
    {
        Modalidade m = Criar(
            natureza: "SUPLEMENTAR", composicao: "SUPLEMENTAR_AO_TOTAL",
            regra: "CRUZADO", par: "AC_Q").Value!;

        m.RemanejamentoArgs.Par.Should().Be("AC_Q");
        m.RemanejamentoArgs.Fallback.Should().BeNull(
            "no certame isolado não há ampla concorrência para receber a vaga que nenhuma "
            + "das duas modalidades preencheu — a ausência de fallback declara isso");
        m.RemanejamentoArgs.Destino.Should().BeNull();
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
        Modalidade m = Criar(
            codigo: "PSIQ_INDIGENA", composicao: "DENTRO_DO_VR",
            natureza: "COTA_RESERVADA", regra: "SEGUE_CASCATA").Value!;
        Guid idOriginal = m.Id;

        Result r = m.Atualizar(
            descricao: "Nova descrição", naturezaLegal: "AMPLA", composicaoVagas: "RESIDUAL_DO_VO",
            composicaoOrigem: null, regraRemanejamento: null,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsSuccess.Should().BeTrue();
        m.Codigo.Valor.Should().Be("PSIQ_INDIGENA", "o código é imutável");
        m.Id.Should().Be(idOriginal, "o Id é imutável");
        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.Descricao.Should().Be("Nova descrição");
    }

    [Fact(DisplayName = "Atualizar revalida coerência (cota sem cascata falha)")]
    public void Atualizar_Incoerente_Falha()
    {
        Modalidade m = Criar(codigo: "PSVR_AMPLA", natureza: "AMPLA").Value!;

        Result r = m.Atualizar(
            descricao: null, naturezaLegal: "COTA_RESERVADA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: null,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    // ── Proteção do catálogo legal fixo ────────────────────────────────────────

    /// <summary>Cota federal tal como semeada: reservada, dentro das vagas reservadas, cascata.</summary>
    private static Modalidade CotaFederal(IReadOnlyList<string>? criterios = null) =>
        Criar(codigo: "LB_PPI", descricao: "Cota", natureza: "COTA_RESERVADA",
            composicao: "DENTRO_DO_VR", regra: "SEGUE_CASCATA", criterios: criterios,
            baseLegal: "Lei 12.711/2012").Value!;

    /// <summary>Pessoa com deficiência na ampla concorrência: única fixa com argumento de remanejamento.</summary>
    private static Modalidade AcPcd() =>
        Criar(codigo: "AC_PCD", natureza: "OUTRA_MODALIDADE", composicao: "RETIRA_DE",
            origem: "AC", regra: "DESTINO_UNICO", destino: "AC").Value!;

    [Fact(DisplayName = "Legal fixa recusa alteração de natureza")]
    public void Atualizar_LegalFixaNatureza_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: "Cota", naturezaLegal: "AMPLA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: "Lei 12.711/2012");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.NaturezaLegal.Should().Be(NaturezaLegal.CotaReservada, "a recusa não muta a entidade");
    }

    [Fact(DisplayName = "Legal fixa recusa alteração de composição de vagas")]
    public void Atualizar_LegalFixaComposicao_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: "Cota", naturezaLegal: "COTA_RESERVADA", composicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            composicaoOrigem: null, regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: "Lei 12.711/2012");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.ComposicaoVagas.Should().Be(ComposicaoVagas.DentroDoVr);
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da origem da composição")]
    public void Atualizar_LegalFixaOrigem_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: "Cota", naturezaLegal: "COTA_RESERVADA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: "AC", regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: "Lei 12.711/2012");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.ComposicaoOrigem.Should().BeNull();
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da regra de remanejamento")]
    public void Atualizar_LegalFixaRegra_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: "Cota", naturezaLegal: "COTA_RESERVADA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: null,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: "Lei 12.711/2012");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.RegraRemanejamento.Should().Be(RegraRemanejamento.SegueCascata);
    }

    [Fact(DisplayName = "Legal fixa recusa alteração dos critérios cumulativos")]
    public void Atualizar_LegalFixaCriterios_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: "Cota", naturezaLegal: "COTA_RESERVADA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: ["RENDA_PER_CAPITA"], acaoQuandoIndeferido: null,
            baseLegal: "Lei 12.711/2012");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.CriteriosCumulativos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Legal fixa recusa alteração da ação quando indeferido")]
    public void Atualizar_LegalFixaAcaoIndeferimento_Recusa()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: "Cota", naturezaLegal: "COTA_RESERVADA", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: "RECLASSIFICAR_AC",
            baseLegal: "Lei 12.711/2012");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.AcaoQuandoIndeferido.Should().BeNull();
    }

    [Theory(DisplayName = "Legal fixa recusa alteração de cada argumento de remanejamento")]
    [InlineData("LB_PPI", null, null)]
    [InlineData("AC", "AC", null)]
    [InlineData("AC", null, "AC")]
    public void Atualizar_LegalFixaArgumentos_Recusa(string? destino, string? par, string? fallback)
    {
        Modalidade m = AcPcd();

        Result r = m.Atualizar(
            descricao: null, naturezaLegal: "OUTRA_MODALIDADE", composicaoVagas: "RETIRA_DE",
            composicaoOrigem: "AC", regraRemanejamento: "DESTINO_UNICO",
            remanejamentoDestino: destino, remanejamentoPar: par, remanejamentoFallback: fallback,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        m.RemanejamentoArgs.Destino.Should().Be("AC");
        m.RemanejamentoArgs.Par.Should().BeNull();
        m.RemanejamentoArgs.Fallback.Should().BeNull();
    }

    [Fact(DisplayName = "Legal fixa aceita alteração de descrição e base legal com a estrutura reenviada idêntica")]
    public void Atualizar_LegalFixaSomenteRedacao_Aceita()
    {
        // Critérios não vazios: com lista vazia os dois lados podem cair na mesma
        // instância cacheada e o teste não distinguiria comparação por valor de
        // comparação por referência.
        Modalidade m = CotaFederal(criterios: ["RENDA_PER_CAPITA"]);

        Result r = m.Atualizar(
            descricao: "Cota — baixa renda, preto/pardo/indígena", naturezaLegal: "COTA_RESERVADA",
            composicaoVagas: "DENTRO_DO_VR", composicaoOrigem: null, regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: ["RENDA_PER_CAPITA"], acaoQuandoIndeferido: null,
            baseLegal: "Lei 12.711/2012 (red. Lei 14.723/2023)");

        r.IsSuccess.Should().BeTrue();
        m.Descricao.Should().Be("Cota — baixa renda, preto/pardo/indígena");
        m.BaseLegal.Should().Be("Lei 12.711/2012 (red. Lei 14.723/2023)");
        m.NaturezaLegal.Should().Be(NaturezaLegal.CotaReservada);
    }

    [Fact(DisplayName = "Modalidade institucional segue alterando a estrutura livremente")]
    public void Atualizar_Institucional_AlteraEstrutura()
    {
        Modalidade m = Criar(
            codigo: "PSIQ_QUILOMBOLA", natureza: "COTA_RESERVADA",
            composicao: "DENTRO_DO_VR", regra: "SEGUE_CASCATA").Value!;

        Result r = m.Atualizar(
            descricao: null, naturezaLegal: "SUPLEMENTAR", composicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            composicaoOrigem: null, regraRemanejamento: "DESTINO_UNICO",
            remanejamentoDestino: "AC", remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsSuccess.Should().BeTrue();
        m.NaturezaLegal.Should().Be(NaturezaLegal.Suplementar);
    }

    [Fact(DisplayName = "Token inválido numa legal fixa reporta o erro de domínio, não a proteção")]
    public void Atualizar_LegalFixaTokenInvalido_ReportaErroDeDominio()
    {
        Modalidade m = CotaFederal();

        Result r = m.Atualizar(
            descricao: null, naturezaLegal: "XPTO", composicaoVagas: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamento: "SEGUE_CASCATA",
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            criteriosCumulativos: null, acaoQuandoIndeferido: null, baseLegal: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaInvalida,
            "não se compara com o cânone aquilo que sequer resolve para um valor de domínio");
    }

    [Fact(DisplayName = "A factory materializa códigos legais fixos — a reserva é do cadastro, não do domínio")]
    public void Criar_CodigoLegalFixo_Aceita()
    {
        Result<Modalidade> r = Criar(codigo: "LB_PPI", natureza: "COTA_RESERVADA",
            composicao: "DENTRO_DO_VR", regra: "SEGUE_CASCATA");

        r.IsSuccess.Should().BeTrue("o seed e a reidratação dependem da factory aceitá-los");
    }

    // ── Acumulação e gate entre campos independentes (ADR-0125) ────────────────

    [Fact(DisplayName = "Código ausente, descrição longa e natureza inválida acumulam as três violações")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<Modalidade> r = Criar(
            codigo: "", descricao: new string('a', 301), natureza: "XPTO");

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(3);
        r.Errors[0].Field.Should().Be("codigo");
        r.Errors[0].Error.Code.Should().Be(ModalidadeErrorCodes.CodigoObrigatorio);
        r.Errors[1].Field.Should().Be("descricao");
        r.Errors[2].Field.Should().Be("naturezaLegal");
    }

    [Fact(DisplayName = "Regra fora do domínio com argumento presente reporta só o token inválido, não a incoerência derivada")]
    public void Criar_RegraInvalidaComArgumento_NaoDerivaIncoerenciaDeArgumento()
    {
        Result<Modalidade> r = Criar(
            natureza: "SUPLEMENTAR", composicao: "SUPLEMENTAR_AO_TOTAL", regra: "CASCATA", destino: "AC");

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(1,
            "a invariante de argumentos por regra depende de RegraRemanejamento já reconhecida — " +
            "reportá-la sobre um token que nem chegou a resolver seria um erro derivado, não independente");
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.RegraRemanejamentoInvalida);
    }

    [Fact(DisplayName = "ValidarCampos isolado é público e reusável sem instanciar o agregado")]
    public void ValidarCampos_CamposValidos_Aceita()
    {
        Result<Modalidade.CamposResolvidos> r = Modalidade.ValidarCampos(
            descricao: null, naturezaLegalToken: "AMPLA", composicaoVagasToken: "RESIDUAL_DO_VO",
            composicaoOrigem: null, regraRemanejamentoToken: null, remanejamentoDestino: null,
            remanejamentoPar: null, remanejamentoFallback: null, criteriosCumulativos: null,
            acaoQuandoIndeferidoToken: null, baseLegal: null);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ValidarCamposDoPayload não avalia coerência cruzada — é só o pré-check do handler")]
    public void ValidarCamposDoPayload_PayloadIncoerenteMasComTokensValidos_Aceita()
    {
        // AMPLA + SEGUE_CASCATA é incoerente (invariante 3), mas ValidarCamposDoPayload
        // não avalia coerência cruzada — só parse e tamanhos. A guarda do catálogo
        // legal fixo (handler) precisa rodar ANTES dessa incoerência ser reportada.
        Result r = Modalidade.ValidarCamposDoPayload(
            descricao: null, naturezaLegalToken: "AMPLA", composicaoVagasToken: "DENTRO_DO_VR",
            composicaoOrigem: null, regraRemanejamentoToken: "SEGUE_CASCATA", remanejamentoDestino: null,
            remanejamentoPar: null, remanejamentoFallback: null, criteriosCumulativos: null,
            acaoQuandoIndeferidoToken: null, baseLegal: null);

        r.IsSuccess.Should().BeTrue();
    }
}
