namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using System.Text.Json;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirFatosColetadosCommandValidatorTests
{
    private static readonly DefinirFatosColetadosCommandValidator Validator = new();

    private static CondicaoPrecondicaoInput Condicao(string fato) =>
        new(fato, "IGUAL", JsonSerializer.SerializeToElement("PRETA"));

    private static DefinirFatosColetadosCommand Comando(params FatoColetadoInput[] itens) =>
        new(Guid.CreateVersion7(), PapelDoFormularioDeTeste.Inscricao.Codigo(), itens);

    private static EntradaDoFatoColetadoDeTeste Item(string fatoCodigo, string rotulo, string tipo = "SIM_OU_NAO") =>
        EntradaDoFatoColetadoDeTeste.Coletando(fatoCodigo).ComRotulo(rotulo).DoTipo(tipo);

    [Fact(DisplayName = "Passa com lista de fatos vazia — zera a coleta")]
    public void Aceita_ListaVazia()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), PapelDoFormularioDeTeste.Inscricao.Codigo(), []));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Passa com fato sem exibição (null)")]
    public void Aceita_FatoSemExibicao()
    {
        ValidationResult result = Validator.Validate(Comando(Item("COR_RACA", "Cor ou raça", "ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir()));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha quando a lista de fatos é nula (payload malformado)")]
    public void Rejeita_FatosNulo()
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), PapelDoFormularioDeTeste.Inscricao.Codigo(), null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens");
    }

    [Fact(DisplayName = "Passa com código do fato vazio no validator — a rejeição é do agregado (FatoColetado.Criar)")]
    public void Aceita_FatoCodigoVazioNoValidator() =>
        Validator.Validate(Comando(Item(string.Empty, "Rótulo", "ESCOLHA_UNICA").Construir())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Passa com rótulo vazio no validator — a rejeição é do agregado (FatoColetado.Criar)")]
    public void Aceita_RotuloVazioNoValidator() =>
        Validator.Validate(Comando(Item("COR_RACA", string.Empty, "ESCOLHA_UNICA").Construir())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Passa com rótulo acima de 300 caracteres no validator — o limite é conferido pelo agregado (FatoColetado.Criar)")]
    public void Aceita_RotuloExcedeLimiteNoValidator() =>
        Validator.Validate(Comando(Item("COR_RACA", new string('a', 301), "ESCOLHA_UNICA").Construir())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Passa com ordem negativa no validator — a rejeição é do agregado (FatoColetado.Criar)")]
    public void Aceita_OrdemNegativaNoValidator() =>
        Validator.Validate(Comando(EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA", -1).Construir())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Passa com apresentação e regras ausentes no validator — a recusa delas é do domínio, com o código de cada uma")]
    public void Aceita_ApresentacaoERegrasAusentesNoValidator() =>
        Validator.Validate(Comando(Item("COR_RACA", "Cor ou raça").SemApresentacao().SemRegras().Construir())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Falha quando a exibição presente é uma lista externa vazia (ausência é null)")]
    public void Rejeita_ExibicaoListaExternaVazia()
    {
        ValidationResult result = Validator.Validate(Comando(Item("BAIXA_RENDA", "Baixa renda").ExibidoQuando([]).Construir()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Regras.Exibicao");
    }

    [Fact(DisplayName = "Falha quando a exibição tem uma cláusula interna vazia")]
    public void Rejeita_ExibicaoClausulaVazia()
    {
        ValidationResult result = Validator.Validate(Comando(Item("BAIXA_RENDA", "Baixa renda").ExibidoQuando([[]]).Construir()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Regras.Exibicao");
    }

    [Fact(DisplayName = "Falha quando a exibição tem uma condição nula ([[null]])")]
    public void Rejeita_ExibicaoCondicaoNula()
    {
        ValidationResult result = Validator.Validate(Comando(Item("BAIXA_RENDA", "Baixa renda").ExibidoQuando([[null!]]).Construir()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Regras.Exibicao");
    }

    [Fact(DisplayName = "Falha quando o predicado da obrigatoriedade presente é uma lista vazia")]
    public void Rejeita_PredicadoDaObrigatoriedadeVazio()
    {
        ValidationResult result = Validator.Validate(Comando(Item("BAIXA_RENDA", "Baixa renda").ComObrigatoriedade("QUANDO", []).Construir()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Itens[0].Regras.Obrigatoriedade.Predicado");
    }

    [Fact(DisplayName = "Passa com exibição bem-formada (uma cláusula, uma condição)")]
    public void Aceita_ExibicaoBemFormada() =>
        Validator.Validate(Comando(Item("BAIXA_RENDA", "Baixa renda").ExibidoQuando([[Condicao("COR_RACA")]]).Construir())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Acima do teto de itens, os itens não são conferidos um a um — a recusa é a da quantidade")]
    public void AcimaDoTeto_NaoConfereItemAItem()
    {
        FatoColetadoInput[] itens = [.. Enumerable.Repeat<FatoColetadoInput>(null!, FormaDoItem.MaximoDeItens + 1)];

        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(Guid.CreateVersion7(), PapelDoFormularioDeTeste.Inscricao.Codigo(), itens));

        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Grupo nulo, sem lista de campos, com campo nulo ou com predicado vazio é recusado na forma")]
    [InlineData("grupoNulo")]
    [InlineData("subitensNulo")]
    [InlineData("campoNulo")]
    [InlineData("exibicaoVazia")]
    [InlineData("obrigatoriedadeVazia")]
    [InlineData("exibicaoDoCampoVazia")]
    [InlineData("obrigatoriedadeDoCampoVazia")]
    public void Rejeita_GrupoMalformado(string caso)
    {
        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), PapelDoFormularioDeTeste.Inscricao.Codigo(), [], [GrupoAdulterado(caso)!]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().OnlyContain(e => e.PropertyName.StartsWith("Grupos[0]", StringComparison.Ordinal));
    }

    private static GrupoColetadoInput? GrupoAdulterado(string caso)
    {
        FatoColetadoInput campo = Item("PARENTESCO", "Parentesco", "ESCOLHA_UNICA").Construir();
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").NaSecao("DADOS")
            .ComOcorrencias(0, 5).ComCampos(campo).Construir();
        return caso switch
        {
            "grupoNulo" => null,
            "subitensNulo" => grupo with { Subitens = null! },
            "campoNulo" => grupo with { Subitens = [null!] },
            "exibicaoVazia" => grupo with { Regras = grupo.Regras! with { Exibicao = [] } },
            "obrigatoriedadeVazia" => grupo with { Regras = grupo.Regras! with { Obrigatoriedade = new ObrigatoriedadeInput("NUNCA", []) } },
            "exibicaoDoCampoVazia" => grupo with { Subitens = [campo with { Regras = campo.Regras! with { Exibicao = [[]] } }] },
            _ => grupo with { Subitens = [campo with { Regras = campo.Regras! with { Obrigatoriedade = new ObrigatoriedadeInput("QUANDO", []) } }] },
        };
    }

    [Fact(DisplayName = "Acima do teto, contando os grupos, o validador não lista um erro por grupo — a recusa é a da quantidade")]
    public void AcimaDoTeto_NaoConfereGrupoAGrupo()
    {
        GrupoColetadoInput[] grupos = [.. Enumerable.Repeat<GrupoColetadoInput>(null!, FormaDoItem.MaximoDeItens + 1)];

        ValidationResult result = Validator.Validate(new DefinirFatosColetadosCommand(
            Guid.CreateVersion7(), PapelDoFormularioDeTeste.Inscricao.Codigo(), [], grupos));

        result.IsValid.Should().BeTrue();
    }
}
