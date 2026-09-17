namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirEtapasCommandValidatorTests
{
    private static readonly Guid TipoEtapaOrigemIdValido = Guid.CreateVersion7();

    private static EtapaProcessoInput EtapaValida() =>
        new("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaOrigemIdValido, 3m, null, 1,
            Produtos: [], Bancas: [], Recursos: []);

    [Fact(DisplayName = "Validator passa com ao menos uma etapa válida")]
    public void Aceita_ComandoValido()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [EtapaValida()], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// A guarda de argumento de <c>ProdutoDaEtapa.Criar</c> LANÇA com código vazio. Sem a
    /// regra de forma aqui, o corpo malformado chegaria lá e viraria falha de servidor, em vez
    /// da recusa de validação que nomeia o campo — como já acontece nos produtos da fase.
    /// </summary>
    /// <summary>
    /// Item nulo dentro das coleções aninhadas passa incólume pelo `ChildRules`, e o handler o
    /// desreferencia — o papel do produto, o tipo da banca, a âncora do recurso. É a mesma
    /// proteção que o array de etapas já tinha, um nível abaixo.
    /// </summary>
    /// <summary>
    /// <c>[JsonRequired]</c> recusa a carga que OMITE a chave, mas <c>"produtos": null</c>
    /// atravessa a desserialização — e, como a gravação substitui a coleção inteira, o nulo
    /// apagaria em silêncio o que a etapa declara. É esta regra que o transforma em recusa
    /// nomeada, em vez de deixá-lo virar lista vazia no handler.
    /// </summary>
    [Theory(DisplayName = "Validator recusa coleção nula na etapa — o nulo não vira lista vazia")]
    [InlineData("produtos", "Etapas[0].Produtos")]
    [InlineData("bancas", "Etapas[0].Bancas")]
    [InlineData("recursos", "Etapas[0].Recursos")]
    public void Rejeita_ColecaoNula(string colecao, string propriedadeEsperada)
    {
        EtapaProcessoInput etapa = colecao switch
        {
            "produtos" => EtapaValida() with { Produtos = null! },
            "bancas" => EtapaValida() with { Bancas = null! },
            _ => EtapaValida() with { Recursos = null! },
        };

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == propriedadeEsperada);
    }

    /// <summary>
    /// A lista vazia é a declaração explícita de que a etapa não tem nenhum — o contraponto
    /// da recusa acima, e o que impede que exigir a chave vire exigir conteúdo.
    /// </summary>
    [Fact(DisplayName = "Validator aceita as três coleções vazias — vazio é declaração, não omissão")]
    public void Aceita_ColecoesVazias()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [EtapaValida()], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Validator recusa item nulo nas coleções da etapa")]
    [InlineData("produtos")]
    [InlineData("bancas")]
    [InlineData("recursos")]
    public void Rejeita_ItemNuloNasColecoesAninhadas(string colecao)
    {
        EtapaProcessoInput etapa = colecao switch
        {
            "produtos" => EtapaValida() with { Produtos = [null!] },
            "bancas" => EtapaValida() with { Bancas = [null!] },
            _ => EtapaValida() with { Recursos = [null!] },
        };

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("não pode ser nulo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Validator recusa produto da etapa sem código de ato")]
    public void Rejeita_ProdutoSemAtoCodigo()
    {
        EtapaProcessoInput etapa = EtapaValida() with
        {
            Produtos = [new ProdutoDaEtapaInput(string.Empty, "PRELIMINAR")],
        };

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("código do ato", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Validator aceita produto da etapa com código declarado")]
    public void Aceita_ProdutoComAtoCodigo()
    {
        EtapaProcessoInput etapa = EtapaValida() with
        {
            Produtos = [new ProdutoDaEtapaInput("RESULTADO_PRELIMINAR", "PRELIMINAR")],
        };

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator aceita lista de etapas vazia (Story #851 §3.5 — processo sem prova é válido)")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando a lista de etapas é nula")]
    public void Rejeita_ListaNula()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), null!, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas");
    }

    [Fact(DisplayName = "Validator passa com caráter Nenhum — a rejeição é do agregado (EtapaProcesso.Criar, ADR-0125)")]
    public void Aceita_CaraterNenhumNoValidator()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Nenhum, TipoEtapaOrigemIdValido, 3m, null, 1,
            Produtos: [], Bancas: [], Recursos: []);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// issue #1071 — cenário 3: tipo é obrigatório desde a definição da etapa. Sem produção em
    /// nenhum ambiente, não há transição com tipo opcional (ver ADR-0123).
    /// </summary>
    [Fact(DisplayName = "Validator falha quando TipoEtapaOrigemId não é informado")]
    public void Rejeita_TipoEtapaOrigemIdVazio()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Classificatoria, Guid.Empty, 3m, null, 1,
            Produtos: [], Bancas: [], Recursos: []);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].TipoEtapaOrigemId");
    }

    [Fact(DisplayName = "Validator passa com peso não positivo — a rejeição é do agregado (EtapaProcesso.Criar, ADR-0125)")]
    public void Aceita_PesoNaoPositivoNoValidator()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaOrigemIdValido, 0m, null, 1,
            Produtos: [], Bancas: [], Recursos: []);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando o peso tem mais de 4 casas decimais (arredondaria para zero)")]
    public void Rejeita_PesoComEscalaExcessiva()
    {
        // 0.00001 > 0 mas numeric(18,4) arredondaria para 0.0000 — divisor da média viraria zero.
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaOrigemIdValido, 0.00001m, null, 1,
            Produtos: [], Bancas: [], Recursos: []);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].Peso");
    }

    [Fact(DisplayName = "Validator falha quando a nota mínima tem mais de 4 casas decimais")]
    public void Rejeita_NotaMinimaComEscalaExcessiva()
    {
        EtapaProcessoInput etapa = new("Prova Objetiva", CaraterEtapa.Eliminatoria, TipoEtapaOrigemIdValido, 3m, 5.00001m, 1,
            Produtos: [], Bancas: [], Recursos: []);

        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [etapa], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Etapas[0].NotaMinima");
    }

    [Fact(DisplayName = "Validator falha (sem estourar) quando o array de etapas tem item nulo")]
    public void Rejeita_ItemNulo()
    {
        ValidationResult result = new DefinirEtapasCommandValidator()
            .Validate(new DefinirEtapasCommand(Guid.CreateVersion7(), [null!], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
    }
}
