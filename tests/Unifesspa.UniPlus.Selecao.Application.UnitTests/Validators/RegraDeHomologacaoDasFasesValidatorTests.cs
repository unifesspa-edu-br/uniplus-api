namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

/// <summary>
/// A validação de forma da regra de homologação recusa só o item nulo das listas, que a montagem desreferenciaria; o
/// resto da regra é recusa do domínio e da montagem, acumulada por campo.
/// </summary>
public sealed class RegraDeHomologacaoDasFasesValidatorTests
{
    private static ValidationResult Validar(RegraDeHomologacaoInput? regra) => new RegraDeHomologacaoDasFasesValidator().Validate(
        new DefinirCronogramaFasesCommand(Guid.CreateVersion7(), [Fase(regra)]));

    private static FaseCronogramaInput Fase(RegraDeHomologacaoInput? regra) => new(
        Ordem: 1, FaseCanonicaId: Guid.CreateVersion7(), Inicio: null, Fim: null, Produtos: [], EmiteParecerIndividual: false, BancasRequeridas: [], RegraRecurso: null, RegraDeHomologacao: regra);

    [Fact(DisplayName = "A fase sem regra de homologação não tem o que validar")]
    public void SemRegra_Aceita() => Validar(regra: null).IsValid.Should().BeTrue();

    [Fact(DisplayName = "A regra vazia, com alternativas e condições ausentes, atravessa a validação de forma")]
    public void RegraSemAlternativasNemCondicoes_AtravessaParaODominioConferir()
    {
        Validar(new RegraDeHomologacaoInput()).IsValid.Should().BeTrue();
        Validar(new RegraDeHomologacaoInput("RESULTADO", [new AlternativaDeCondicoesInput()])).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Alternativa nula é recusada na posição dela")]
    public void AlternativaNula_Recusa()
    {
        ValidationResult resultado = Validar(new RegraDeHomologacaoInput("RESULTADO", [null!]));

        resultado.Errors.Should().ContainSingle().Which.PropertyName.Should().Contain("Alternativas[0]");
    }

    [Fact(DisplayName = "Condição nula é recusada na posição dela")]
    public void CondicaoNula_Recusa()
    {
        ValidationResult resultado = Validar(new RegraDeHomologacaoInput("RESULTADO", [new AlternativaDeCondicoesInput([new CondicaoDeHomologacaoInput(), null!])]));

        resultado.Errors.Should().ContainSingle().Which.PropertyName.Should().Contain("Condicoes[1]");
    }
}
