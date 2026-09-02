namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoBancaTests
{
    private static Result<TipoBanca> Criar(
        string codigo = "BANCA_ENTREVISTA",
        string? nome = "Banca de entrevista",
        string? faseTipica = null,
        string? descricao = null) =>
        TipoBanca.Criar(codigo, nome, faseTipica, descricao);

    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Tipo de banca válido preenche os campos e fica ativo com Guid v7")]
    public void Criar_Valido_Aceita()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_DOCUMENTAL", nome: "Banca de análise documental").Value!;

        b.Id.Should().NotBe(Guid.Empty);
        b.Codigo.Valor.Should().Be("BANCA_ANALISE_DOCUMENTAL");
        b.Nome.Should().Be("Banca de análise documental");
        b.FaseTipica.Should().BeNull();
        b.IsDeleted.Should().BeFalse();
    }

    // ── Formato do código ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Código com minúscula, hífen ou dígito é rejeitado (formato)")]
    [InlineData("banca_entrevista")]
    [InlineData("BANCA-ENTREVISTA")]
    [InlineData("BANCA2")]
    public void Criar_CodigoForaDoFormato_Falha(string codigo)
    {
        Result<TipoBanca> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<TipoBanca> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoObrigatorio);
    }

    // ── Domínio canônico ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Código bem-formado fora do conjunto canônico é rejeitado")]
    public void Criar_CodigoForaDoConjuntoCanonico_Falha()
    {
        Result<TipoBanca> r = Criar(codigo: "BANCA_LOGISTICA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Fact(DisplayName = "Mensagem de código fora do conjunto canônico não ecoa o valor submetido")]
    public void Criar_CodigoForaDoConjuntoCanonico_NaoEcoaValorSubmetidoNaMensagem()
    {
        Result<TipoBanca> r = Criar(codigo: "BANCA_LOGISTICA_MUITO_ESPECIFICA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().NotContain("BANCA_LOGISTICA_MUITO_ESPECIFICA");
    }

    [Theory(DisplayName = "Códigos dentro do conjunto canônico são aceitos")]
    [InlineData("BANCA_ANALISE_DOCUMENTAL")]
    [InlineData("BANCA_CORRECAO_REDACOES")]
    [InlineData("BANCA_ANALISE_RECURSOS")]
    [InlineData("BANCA_HETEROIDENTIFICACAO")]
    [InlineData("BANCA_BIOPSICOSSOCIAL")]
    public void Criar_CodigoCanonico_Aceita(string codigo)
    {
        Result<TipoBanca> r = Criar(codigo: codigo);

        r.IsSuccess.Should().BeTrue();
    }

    // ── Nome ───────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Nome ausente é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<TipoBanca> r = Criar(nome: nome);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome acima de 200 caracteres é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<TipoBanca> r = Criar(nome: new string('a', 201));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.NomeTamanho);
    }

    // ── Fase típica (orientativa, não vinculante) ──────────────────────────────

    [Fact(DisplayName = "Banca sem fase típica é aceita (fase típica nula)")]
    public void Criar_SemFaseTipica_Aceita()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_RECURSOS", nome: "Banca de análise de recursos", faseTipica: null).Value!;

        b.FaseTipica.Should().BeNull();
    }

    [Fact(DisplayName = "Fase típica é rótulo orientativo — valor sem correspondência é aceito")]
    public void Criar_FaseTipicaNaoVinculante_Aceita()
    {
        TipoBanca b = Criar(faseTipica: "Fase que não corresponde a nenhum código de fase").Value!;

        b.FaseTipica.Should().Be("Fase que não corresponde a nenhum código de fase");
    }

    [Fact(DisplayName = "Fase típica acima de 60 caracteres é rejeitada")]
    public void Criar_FaseTipicaLonga_Falha()
    {
        Result<TipoBanca> r = Criar(faseTipica: new string('a', 61));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.FaseTipicaTamanho);
    }

    /// <summary>
    /// O limite vale sobre o valor já normalizado (Trim), não sobre o bruto — 60
    /// caracteres úteis mais espaços nas pontas continua válido.
    /// </summary>
    [Fact(DisplayName = "Fase típica com 60 caracteres úteis mais espaços nas pontas é aceita")]
    public void Criar_FaseTipicaNoLimiteComEspacosNasPontas_Aceita()
    {
        TipoBanca b = Criar(faseTipica: "  " + new string('a', 60) + "  ").Value!;

        b.FaseTipica.Should().Be(new string('a', 60));
    }

    // ── Descrição ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Descrição acima de 300 caracteres é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<TipoBanca> r = Criar(descricao: new string('a', 301));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.DescricaoTamanho);
    }

    // ── Acumulação (ADR-0125) ──────────────────────────────────────────────────

    /// <summary>
    /// Antes: Criar retornava no primeiro campo inválido (código), mascarando as
    /// demais violações — paridade com o FluentValidation removido, que reportava
    /// cada campo em separado.
    /// </summary>
    [Fact(DisplayName = "Código fora do canônico e nome ausente ao mesmo tempo acumulam as duas violações rotuladas")]
    public void Criar_CodigoForaDoCanonicoENomeAusente_AcumulaAsDuasViolacoesRotuladas()
    {
        Result<TipoBanca> r = TipoBanca.Criar("BANCA_LOGISTICA", "", null, null);

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(2);
        r.Errors[0].Field.Should().Be("codigo");
        r.Errors[0].Error.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
        r.Errors[1].Field.Should().Be("nome");
        r.Errors[1].Error.Code.Should().Be(TipoBancaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome ausente, fase típica e descrição longas acumulam as três violações")]
    public void Criar_NomeAusenteFaseTipicaEDescricaoLongas_AcumulaAsTresViolacoes()
    {
        Result<TipoBanca> r = TipoBanca.Criar(
            "BANCA_ENTREVISTA", "", new string('a', 61), new string('b', 301));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(3);
        r.Errors[0].Field.Should().Be("nome");
        r.Errors[1].Field.Should().Be("faseTipica");
        r.Errors[2].Field.Should().Be("descricao");
    }

    [Fact(DisplayName = "Atualizar com nome ausente e descrição longa acumula as duas violações sem mutar o agregado")]
    public void Atualizar_NomeAusenteEDescricaoLonga_AcumulaAsDuasViolacoesSemMutar()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_RECURSOS", nome: "Banca de análise de recursos").Value!;

        Result r = b.Atualizar(nome: "", faseTipica: null, descricao: new string('a', 301));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(2);
        b.Nome.Should().Be("Banca de análise de recursos", "falha de validação não pode mutar o agregado");
    }

    // ── Imutabilidade / atualização ────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar troca atributos editáveis mantendo Codigo e Id imutáveis")]
    public void Atualizar_MantemCodigoEId()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_RECURSOS", nome: "Banca de análise de recursos").Value!;
        Guid idOriginal = b.Id;

        Result r = b.Atualizar(nome: "Banca de recursos (novo)", faseTipica: "Recursos", descricao: "desc");

        r.IsSuccess.Should().BeTrue();
        b.Codigo.Valor.Should().Be("BANCA_ANALISE_RECURSOS", "o código é imutável");
        b.Id.Should().Be(idOriginal, "o Id é imutável");
        b.Nome.Should().Be("Banca de recursos (novo)");
        b.FaseTipica.Should().Be("Recursos");
    }
}
