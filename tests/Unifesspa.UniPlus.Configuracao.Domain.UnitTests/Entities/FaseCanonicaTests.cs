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
        string? baseLegal = null) =>
        FaseCanonica.Criar(codigo, nome, descricao, dono, agrupaEtapas, permiteComplementacao, baseLegal);

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
    [InlineData("HOMOLOGACAO_RESULTADO_FINAL")]
    [InlineData("CHAMADA")]
    public void Criar_CodigoCanonico_Aceita(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsSuccess.Should().BeTrue();
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
            agrupaEtapas: false, permiteComplementacao: false, baseLegal: null);

        r.IsSuccess.Should().BeTrue();
        f.Codigo.Valor.Should().Be("ENSALAMENTO", "o código é imutável");
        f.Id.Should().Be(idOriginal, "o Id é imutável");
        f.Nome.Should().Be("Ensalamento (novo)");
        f.DonoTipico.Should().Be(DonoTipico.Crca);
    }

    [Fact(DisplayName = "Atualizar revalida coerência de agrupar etapas contra o código congelado")]
    public void Atualizar_AgrupaEtapasIncoerente_Falha()
    {
        FaseCanonica f = Criar(codigo: "HOMOLOGACAO", nome: "Homologação").Value!;

        Result r = f.Atualizar(
            nome: "Homologação", descricao: null, donoTipico: "CEPS",
            agrupaEtapas: true, permiteComplementacao: false, baseLegal: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }
}
