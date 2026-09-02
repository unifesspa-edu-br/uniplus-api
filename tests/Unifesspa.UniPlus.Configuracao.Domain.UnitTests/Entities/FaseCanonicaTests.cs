namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class FaseCanonicaTests
{
    private static Result<FaseCanonica> Criar(
        string codigo = "INSCRICAO",
        string? nome = "Inscrição",
        string? descricao = null,
        string? dono = "CEPS",
        bool agrupaEtapas = false,
        bool permiteComplementacao = false,
        string? baseLegal = null,
        bool produzResultado = false,
        bool resultadoDefinitivo = false,
        bool coletaInscricao = false,
        bool? coletaSolicitacaoIsencao = null,
        string? origemData = "PROPRIA") =>
        FaseCanonica.Criar(
            codigo, nome, descricao, dono, agrupaEtapas, permiteComplementacao, baseLegal,
            produzResultado, resultadoDefinitivo, coletaInscricao,
            // A marca acompanha o código por definição: os testes só a informam quando querem
            // exercitar a divergência.
            coletaSolicitacaoIsencao ?? codigo == FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            origemData);

    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Fase válida preenche os campos e fica ativa com Guid v7")]
    public void Criar_Valida_Aceita()
    {
        FaseCanonica f = Criar(
            codigo: "AVALIACAO", nome: "Avaliação", dono: "CEPS", agrupaEtapas: true).Value!;

        f.Id.Should().NotBe(Guid.Empty);
        f.Codigo.Valor.Should().Be("AVALIACAO");
        f.Nome.Should().Be("Avaliação");
        f.DonoTipico.Should().Be(DonoTipico.Ceps);
        f.AgrupaEtapas.Should().BeTrue();
        f.PermiteComplementacao.Should().BeFalse();
        f.IsDeleted.Should().BeFalse();
    }

    // ── Formato do código ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Código com minúscula, hífen, dígito ou espaço é rejeitado (formato)")]
    [InlineData("inscricao")]
    [InlineData("RESULTADO-FINAL")]
    [InlineData("FASE2")]
    [InlineData("LISTA ESPERA")]
    public void Criar_CodigoForaDoFormato_Falha(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoObrigatorio);
    }

    // ── Domínio canônico ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Código bem-formado fora do conjunto canônico é rejeitado")]
    public void Criar_CodigoForaDoConjuntoCanonico_Falha()
    {
        Result<FaseCanonica> r = Criar(codigo: "ENTREVISTA_FINAL");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Theory(DisplayName = "Códigos dentro do conjunto canônico são aceitos")]
    [InlineData("HETEROIDENTIFICACAO")]
    [InlineData("AVALIACAO_BIOPSICOSSOCIAL")]
    [InlineData("HOMOLOGACAO_RESULTADO_FINAL")]
    [InlineData("CHAMADA")]
    [InlineData("SOLICITACAO_ISENCAO")]
    public void Criar_CodigoCanonico_Aceita(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsSuccess.Should().BeTrue();
    }

    // ── Solicitação de isenção (UNI-REQ-0106) ──────────────────────────────────

    [Fact(DisplayName = "Constante da solicitação de isenção pertence ao rol canônico")]
    public void Catalogo_CodigoSolicitacaoIsencao_EhCanonico()
    {
        FaseCanonicaCatalogo.Codigos.Should().Contain(FaseCanonicaCatalogo.CodigoSolicitacaoIsencao);
        FaseCanonicaCatalogo.EhCanonico(FaseCanonicaCatalogo.CodigoSolicitacaoIsencao).Should().BeTrue();
    }

    [Fact(DisplayName = "Solicitação de isenção produz resultado não definitivo — o indeferimento admite recurso")]
    public void Criar_SolicitacaoIsencao_ProduzResultadoNaoDefinitivo_Aceita()
    {
        FaseCanonica f = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            nome: "Solicitação de isenção",
            produzResultado: true,
            resultadoDefinitivo: false).Value!;

        f.ProduzResultado.Should().BeTrue();
        f.ResultadoDefinitivo.Should().BeFalse();
        f.ColetaInscricao.Should().BeFalse();
    }

    [Fact(DisplayName = "Fase de isenção sem a marca é recusada — sem ela a janela publica sem validação")]
    public void Criar_SolicitacaoIsencaoSemAMarca_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, coletaSolicitacaoIsencao: false);

        r.IsFailure.Should().BeTrue();
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao);
    }

    [Fact(DisplayName = "Solicitação de isenção não agrupa etapas — só a avaliação agrupa")]
    public void Criar_SolicitacaoIsencaoAgrupandoEtapas_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, agrupaEtapas: true);

        r.IsFailure.Should().BeTrue();
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }

    [Fact(DisplayName = "Solicitação de isenção não permite complementação documental")]
    public void Criar_SolicitacaoIsencaoComComplementacao_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, permiteComplementacao: true);

        r.IsFailure.Should().BeTrue();
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas);
    }

    // ── Nome ───────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Nome ausente é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<FaseCanonica> r = Criar(nome: nome);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome acima de 200 caracteres é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<FaseCanonica> r = Criar(nome: new string('a', 201));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NomeTamanho);
    }

    // ── Dono típico ────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Dono típico em domínio é aceito")]
    [InlineData("CEPS", DonoTipico.Ceps)]
    [InlineData("CRCA", DonoTipico.Crca)]
    [InlineData("MEC", DonoTipico.Mec)]
    [InlineData("CONSEPE", DonoTipico.Consepe)]
    public void Criar_DonoTipicoValido_Aceita(string token, DonoTipico esperado)
    {
        FaseCanonica f = Criar(codigo: "MATRICULA", nome: "Matrícula", dono: token).Value!;

        f.DonoTipico.Should().Be(esperado);
    }

    [Theory(DisplayName = "Dono típico ausente é rejeitado (obrigatório)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemDonoTipico_Falha(string dono)
    {
        Result<FaseCanonica> r = Criar(dono: dono);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.DonoTipicoObrigatorio);
    }

    [Theory(DisplayName = "Dono típico fora do domínio (incl. numérico e PascalCase) é rejeitado")]
    [InlineData("DTI")]
    [InlineData("1")]
    [InlineData("Ceps")]
    public void Criar_DonoTipicoInvalido_Falha(string dono)
    {
        Result<FaseCanonica> r = Criar(dono: dono);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.DonoTipicoInvalido);
    }

    // ── Origem da data ─────────────────────────────────────────────────────────

    [Theory(DisplayName = "Origem da data em domínio é aceita")]
    [InlineData("PROPRIA", OrigemDataFase.Propria)]
    [InlineData("DELEGADA", OrigemDataFase.Delegada)]
    public void Criar_OrigemDataValida_Aceita(string token, OrigemDataFase esperado)
    {
        FaseCanonica f = Criar(codigo: "MATRICULA", nome: "Matrícula", origemData: token).Value!;

        f.OrigemData.Should().Be(esperado);
    }

    [Theory(DisplayName = "Origem da data ausente é rejeitada (obrigatória)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemOrigemData_Falha(string origemData)
    {
        Result<FaseCanonica> r = Criar(origemData: origemData);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.OrigemDataObrigatoria);
    }

    [Theory(DisplayName = "Origem da data fora do domínio (incl. numérico e minúsculo) é rejeitada")]
    [InlineData("EXTERNA")]
    [InlineData("1")]
    [InlineData("propria")]
    public void Criar_OrigemDataInvalida_Falha(string origemData)
    {
        Result<FaseCanonica> r = Criar(origemData: origemData);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.OrigemDataInvalida);
    }

    // ── Coerência agrupa_etapas ⇒ avaliação ────────────────────────────────────

    [Fact(DisplayName = "Agrupar etapas verdadeiro para a fase de avaliação é aceito")]
    public void Criar_AgrupaEtapasAvaliacao_Aceita()
    {
        Result<FaseCanonica> r = Criar(codigo: "AVALIACAO", nome: "Avaliação", agrupaEtapas: true);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Agrupar etapas verdadeiro para fase que não é avaliação é rejeitado")]
    public void Criar_AgrupaEtapasForaDaAvaliacao_Falha()
    {
        Result<FaseCanonica> r = Criar(codigo: "HOMOLOGACAO", nome: "Homologação", agrupaEtapas: true);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }

    [Fact(DisplayName = "Agrupar etapas é falso por omissão")]
    public void Criar_SemAgrupaEtapas_DefaultFalso()
    {
        FaseCanonica f = Criar(codigo: "HOMOLOGACAO", nome: "Homologação").Value!;

        f.AgrupaEtapas.Should().BeFalse();
    }

    // ── Coerência permite_complementacao ⇒ fases permitidas ────────────────────

    [Theory(DisplayName = "Permitir complementação verdadeiro em fase permitida é aceito")]
    [InlineData("HOMOLOGACAO")]
    [InlineData("RECURSOS")]
    public void Criar_ComplementacaoFasePermitida_Aceita(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo, nome: "Fase", permiteComplementacao: true);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Permitir complementação verdadeiro em fase vedada (habilitação) é rejeitado")]
    public void Criar_ComplementacaoFaseVedada_Falha()
    {
        Result<FaseCanonica> r = Criar(codigo: "HABILITACAO", nome: "Habilitação", permiteComplementacao: true);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas);
    }

    [Fact(DisplayName = "Permitir complementação é falso por omissão")]
    public void Criar_SemComplementacao_DefaultFalso()
    {
        FaseCanonica f = Criar(codigo: "HABILITACAO", nome: "Habilitação").Value!;

        f.PermiteComplementacao.Should().BeFalse();
    }

    // ── Coerência resultado_definitivo ⇒ produz_resultado ──────────────────────

    [Fact(DisplayName = "Resultado definitivo sem produzir resultado é rejeitado")]
    public void Criar_ResultadoDefinitivoSemProduzirResultado_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "RESULTADO_FINAL", nome: "Resultado final",
            produzResultado: false, resultadoDefinitivo: true);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.ResultadoDefinitivoSemProduzirResultado);
    }

    [Fact(DisplayName = "Resultado definitivo com produzir resultado é aceito")]
    public void Criar_ResultadoDefinitivoComProduzirResultado_Aceita()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "RESULTADO_FINAL", nome: "Resultado final",
            produzResultado: true, resultadoDefinitivo: true);

        r.IsSuccess.Should().BeTrue();
        r.Value!.ProduzResultado.Should().BeTrue();
        r.Value!.ResultadoDefinitivo.Should().BeTrue();
    }

    [Fact(DisplayName = "Produzir resultado sem resultado definitivo é aceito (cabe recurso)")]
    public void Criar_ProduzResultadoSemResultadoDefinitivo_Aceita()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "RESULTADO_PRELIMINAR", nome: "Resultado preliminar",
            produzResultado: true, resultadoDefinitivo: false);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Produzir resultado e coletar inscrição são falsos por omissão")]
    public void Criar_SemProduzResultadoNemColetaInscricao_DefaultFalso()
    {
        FaseCanonica f = Criar(codigo: "HABILITACAO", nome: "Habilitação").Value!;

        f.ProduzResultado.Should().BeFalse();
        f.ResultadoDefinitivo.Should().BeFalse();
        f.ColetaInscricao.Should().BeFalse();
    }

    // ── Base legal ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Base legal acima de 500 caracteres é rejeitada")]
    public void Criar_BaseLegalLonga_Falha()
    {
        Result<FaseCanonica> r = Criar(baseLegal: new string('a', 501));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.BaseLegalTamanho);
    }

    // ── Imutabilidade / atualização ────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar troca atributos editáveis mantendo Codigo e Id imutáveis")]
    public void Atualizar_MantemCodigoEId()
    {
        FaseCanonica f = Criar(codigo: "ENSALAMENTO", nome: "Ensalamento").Value!;
        Guid idOriginal = f.Id;

        Result r = f.Atualizar(
            nome: "Ensalamento (novo)", descricao: "Nova descrição", donoTipico: "CRCA",
            agrupaEtapas: false, permiteComplementacao: false, baseLegal: null,
            produzResultado: false, resultadoDefinitivo: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            origemData: "DELEGADA");

        r.IsSuccess.Should().BeTrue();
        f.Codigo.Valor.Should().Be("ENSALAMENTO", "o código é imutável");
        f.Id.Should().Be(idOriginal, "o Id é imutável");
        f.Nome.Should().Be("Ensalamento (novo)");
        f.DonoTipico.Should().Be(DonoTipico.Crca);
        f.OrigemData.Should().Be(OrigemDataFase.Delegada);
    }

    [Fact(DisplayName = "Atualizar revalida coerência de agrupar etapas contra o código congelado")]
    public void Atualizar_AgrupaEtapasIncoerente_Falha()
    {
        FaseCanonica f = Criar(codigo: "HOMOLOGACAO", nome: "Homologação").Value!;

        Result r = f.Atualizar(
            nome: "Homologação", descricao: null, donoTipico: "CEPS",
            agrupaEtapas: true, permiteComplementacao: false, baseLegal: null,
            produzResultado: false, resultadoDefinitivo: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            origemData: "PROPRIA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }

    [Fact(DisplayName = "Atualizar revalida a coerência resultado definitivo sem produzir resultado")]
    public void Atualizar_ResultadoDefinitivoSemProduzirResultado_Falha()
    {
        FaseCanonica f = Criar(codigo: "RESULTADO_FINAL", nome: "Resultado final").Value!;

        Result r = f.Atualizar(
            nome: "Resultado final", descricao: null, donoTipico: "CEPS",
            agrupaEtapas: false, permiteComplementacao: false, baseLegal: null,
            produzResultado: false, resultadoDefinitivo: true, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            origemData: "PROPRIA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.ResultadoDefinitivoSemProduzirResultado);
    }

    // ── Nulo não lança (ADR-0125) e acumulação ──────────────────────────────────

    [Fact(DisplayName = "Código nulo não lança e acumula com nome ausente")]
    public void Criar_CodigoNuloENomeAusente_NaoLancaEAcumulaAsDuasViolacoes()
    {
        Result<FaseCanonica> resultado = FaseCanonica.Criar(
            null, "", null, "CEPS", false, false, null, false, false, false, false, "PROPRIA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.CodigoObrigatorio);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Código fora do conjunto canônico com AgrupaEtapas=true não soma a coerência dependente do código")]
    public void Criar_CodigoInvalidoComAgrupaEtapas_NaoSomaCoerenciaDependente()
    {
        Result<FaseCanonica> resultado = Criar(codigo: "ENTREVISTA_FINAL", agrupaEtapas: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(
            "a coerência AgrupaEtapasApenasAvaliacao depende de um código já confiável — "
            + "com o código inválido, relatá-la seria um segundo erro derivado do primeiro");
        resultado.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Fact(DisplayName = "Nome ausente e resultado definitivo sem produzir resultado (independente do código) acumulam as duas violações")]
    public void Criar_NomeAusenteECA04_AcumulaAsDuasViolacoes()
    {
        Result<FaseCanonica> resultado = Criar(
            nome: "", produzResultado: false, resultadoDefinitivo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("resultadoDefinitivo");
        resultado.Errors[1].Error.Code.Should().Be(FaseCanonicaErrorCodes.ResultadoDefinitivoSemProduzirResultado);
    }

    [Fact(DisplayName = "ValidarCamposComuns isolado acumula violações independentes do código, sem I/O")]
    public void ValidarCamposComuns_TresCamposInvalidos_AcumulaAsTresViolacoes()
    {
        Result<(string Nome, string? Descricao, DonoTipico DonoTipico, string? BaseLegal,
            bool ProduzResultado, bool ResultadoDefinitivo, OrigemDataFase OrigemData)> resultado =
            FaseCanonica.ValidarCamposComuns(null, null, null, null, false, false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("donoTipico");
        resultado.Errors[2].Field.Should().Be("origemData");
    }
}
