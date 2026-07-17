namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="DefinirDocumentosExigidosCommandValidator"/> para a base legal
/// (Story #554, PR-c, issue #549) — valida apenas a FORMA de cada item; o gate "≥1
/// RESOLVIDO por exigência que determina resultado" é do domínio, na publicação.
/// </summary>
public sealed class DefinirDocumentosExigidosCommandValidatorTests
{
    private static ItemDocumentoExigidoInput ItemCom(params BaseLegalInput[] basesLegais) => new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), "GERAL", true, null, null, [], basesLegais);

    private static ValidationResult Validar(ItemDocumentoExigidoInput item) =>
        new DefinirDocumentosExigidosCommandValidator().Validate(
            new DefinirDocumentosExigidosCommand(Guid.CreateVersion7(), [item], PrecondicaoIfMatch.Ausente));

    [Fact(DisplayName = "Item sem base legal (lista vazia) é aceito — a coerência com o gate é da publicação")]
    public void Aceita_SemBaseLegal() =>
        Validar(ItemCom()).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Base legal com dados coerentes é aceita")]
    public void Aceita_BaseLegalValida() =>
        Validar(ItemCom(new BaseLegalInput("Lei 12.711/2012, art. 3º", "FEDERAL", "RESOLVIDO", null))).IsValid.Should().BeTrue();

    [Fact(DisplayName = "CA-01: referência vazia é rejeitada")]
    public void Rejeita_ReferenciaBaseLegalVazia()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("", "FEDERAL", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("referência da base legal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "CA-01: referência só de espaços é rejeitada")]
    public void Rejeita_ReferenciaBaseLegalSoDeEspacos()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("   ", "FEDERAL", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Abrangência fora do domínio é rejeitada")]
    public void Rejeita_AbrangenciaDesconhecida()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("Lei X", "PLANETARIA", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Abrangência", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Status fora do domínio é rejeitado")]
    public void Rejeita_StatusDesconhecido()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("Lei X", "FEDERAL", "EM_ANALISE", null)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Status", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "Todas as abrangências e status válidos são aceitos")]
    [InlineData("FEDERAL", "PENDENTE")]
    [InlineData("ESTADUAL", "RESOLVIDO")]
    [InlineData("MUNICIPAL", "PENDENTE")]
    [InlineData("INTERNA_NORMA", "RESOLVIDO")]
    [InlineData("INTERNA_EDITAL", "RESOLVIDO")]
    public void Aceita_TodasCombinacoesValidas(string abrangencia, string status) =>
        Validar(ItemCom(new BaseLegalInput("Referência", abrangencia, status, "Observação"))).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Achado Codex P2 (PR #898): referência acima de 500 caracteres é rejeitada — o mesmo teto de DocumentoExigidoBaseLegalConfiguration")]
    public void Rejeita_ReferenciaAcimaDoTetoDePersistencia()
    {
        string referenciaLonga = new('a', 501);

        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput(referenciaLonga, "FEDERAL", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("500 caracteres", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Referência com exatamente 500 caracteres é aceita (contraprova do limite)")]
    public void Aceita_ReferenciaNoTetoDePersistencia() =>
        Validar(ItemCom(new BaseLegalInput(new string('a', 500), "FEDERAL", "RESOLVIDO", null))).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Achado Codex P2 (PR #898): observação acima de 1000 caracteres é rejeitada")]
    public void Rejeita_ObservacaoAcimaDoTetoDePersistencia()
    {
        string observacaoLonga = new('a', 1001);

        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("Referência", "FEDERAL", "RESOLVIDO", observacaoLonga)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("1000 caracteres", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Achado Codex P2 (PR #898, 2ª rodada): item de base legal nulo na lista é rejeitado")]
    public void Rejeita_ItemDeBaseLegalNulo()
    {
        ItemDocumentoExigidoInput item = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "GERAL", true, null, null, [], [null!]);

        ValidationResult resultado = Validar(item);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Item de base legal", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Múltiplas bases legais no mesmo item são aceitas (1:N)")]
    public void Aceita_MultiplasBasesLegais() =>
        Validar(ItemCom(
            new BaseLegalInput("Lei Federal X", "FEDERAL", "RESOLVIDO", null),
            new BaseLegalInput("Cláusula do edital", "INTERNA_EDITAL", "PENDENTE", null)))
            .IsValid.Should().BeTrue();
}
