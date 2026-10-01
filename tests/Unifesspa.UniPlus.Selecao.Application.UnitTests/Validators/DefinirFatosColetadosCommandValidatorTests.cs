namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using System.Text.Json;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirFatosColetadosCommandValidatorTests
{
    private static readonly DefinirFatosColetadosCommandValidator Validator = new();

    private static CondicaoPrecondicaoInput Condicao(string fato) =>
        new(fato, "IGUAL", JsonSerializer.SerializeToElement("PRETA"));

    [Fact(DisplayName = "Passa com lista de fatos vazia — zera a coleta")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com fato sem pré-condição (null)")]
    public void Aceita_FatoSemPrecondicao()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a lista de fatos é nula (payload malformado)")]
    public void Rejeita_FatosNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, null!, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens");
    }

    [Fact(DisplayName = "Passa com código do fato vazio no validator — a rejeição é do agregado (FatoColetado.Criar)")]
    public void Aceita_FatoCodigoVazioNoValidator()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("", 0, "Rótulo", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com rótulo vazio no validator — a rejeição é do agregado (FatoColetado.Criar)")]
    public void Aceita_RotuloVazioNoValidator()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("COR_RACA", 0, "", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com rótulo acima de 300 caracteres no validator — o limite é conferido pelo agregado (FatoColetado.Criar)")]
    public void Aceita_RotuloExcedeLimiteNoValidator()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("COR_RACA", 0, new string('a', 301), "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com ordem negativa no validator — a rejeição é do agregado (FatoColetado.Criar)")]
    public void Aceita_OrdemNegativaNoValidator()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("COR_RACA", -1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null)], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a pré-condição presente é uma lista externa vazia (ausência é null)")]
    public void Rejeita_PrecondicaoListaExternaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Precondicao");
    }

    [Fact(DisplayName = "Falha quando a pré-condição tem uma cláusula interna vazia")]
    public void Rejeita_PrecondicaoClausulaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[]])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Precondicao");
    }

    [Fact(DisplayName = "Falha quando a pré-condição tem uma condição nula ([[null]])")]
    public void Rejeita_PrecondicaoCondicaoNula()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[null!]])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Precondicao");
    }

    [Fact(DisplayName = "Passa com pré-condição bem-formada (uma cláusula, uma condição)")]
    public void Aceita_PrecondicaoBemFormada()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA")]])], PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Acima do teto de itens, os itens não são conferidos um a um — a recusa é a da quantidade")]
    public void AcimaDoTeto_NaoConfereItemAItem()
    {
        FatoColetadoInput[] itens = [.. Enumerable.Repeat<FatoColetadoInput>(null!, FormaDoItem.MaximoDeItens + 1)];

        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, itens, PrecondicaoIfMatch.Ausente));

        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Grupo nulo, sem lista de campos, com campo nulo ou com predicado vazio é recusado na forma")]
    [InlineData("grupoNulo")]
    [InlineData("subitensNulo")]
    [InlineData("campoNulo")]
    [InlineData("exibicaoVazia")]
    [InlineData("obrigatoriedadeVazia")]
    [InlineData("precondicaoDoCampoVazia")]
    [InlineData("obrigatoriedadeDoCampoVazia")]
    public void Rejeita_GrupoMalformado(string caso)
    {
        FatoColetadoInput campo = new("PARENTESCO", 0, "Parentesco", "SELECAO_UNICA", "SEMPRE", null);
        GrupoColetadoInput grupo = new("COMPOSICAO", 1, "Composição", "DADOS", 0, 5, null, "NUNCA", null, [campo]);
        GrupoColetadoInput? adulterado = caso switch
        {
            "grupoNulo" => null,
            "subitensNulo" => grupo with { Subitens = null! },
            "campoNulo" => grupo with { Subitens = [null!] },
            "exibicaoVazia" => grupo with { Exibicao = [] },
            "obrigatoriedadeVazia" => grupo with { PredicadoObrigatoriedade = [] },
            "precondicaoDoCampoVazia" => grupo with { Subitens = [campo with { Precondicao = [[]] }] },
            _ => grupo with { Subitens = [campo with { PredicadoObrigatoriedade = [] }] },
        };

        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Ausente, [adulterado!]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().OnlyContain(e => e.PropertyName.StartsWith("Grupos[0]", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Acima do teto, contando os grupos, o validador não lista um erro por grupo — a recusa é a da quantidade")]
    public void AcimaDoTeto_NaoConfereGrupoAGrupo()
    {
        GrupoColetadoInput[] grupos = [.. Enumerable.Repeat<GrupoColetadoInput>(null!, FormaDoItem.MaximoDeItens + 1)];

        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Ausente, grupos));

        result.IsValid.Should().BeTrue();
    }
}
