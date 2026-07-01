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

    [Theory(DisplayName = "Códigos dentro do conjunto canônico são aceitos")]
    [InlineData("BANCA_ANALISE_DOCUMENTAL")]
    [InlineData("BANCA_CORRECAO_REDACOES")]
    [InlineData("BANCA_ANALISE_RECURSOS")]
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
