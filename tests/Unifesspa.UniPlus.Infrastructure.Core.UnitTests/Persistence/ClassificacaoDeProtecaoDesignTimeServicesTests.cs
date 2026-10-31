namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

// O filtro do snapshot das migrations (ADR-0153): a classificação não é schema e não pode ser emitida.
public sealed class ClassificacaoDeProtecaoDesignTimeServicesTests
{
    private static IAnnotationCodeGenerator Decorado(IAnnotationCodeGenerator original)
    {
        ServiceCollection services = new();
        services.AddSingleton(original);
        services.ExcluirClassificacaoDoSnapshot();
        return services.BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();
    }

    private static IAnnotationCodeGenerator Provider()
    {
        IAnnotationCodeGenerator provider = Substitute.For<IAnnotationCodeGenerator>();
        provider.FilterIgnoredAnnotations(Arg.Any<IEnumerable<IAnnotation>>()).Returns(c => c.Arg<IEnumerable<IAnnotation>>());
        return provider;
    }

    [Fact(DisplayName = "O snapshot omite a classificação e mantém as demais anotações")]
    public void OmiteAClassificacaoEMantemAsDemais()
    {
        IAnnotationCodeGenerator gerador = Decorado(Provider());
        Annotation[] anotacoes =
        [
            new("UniPlus:ClassificacaoPadrao", 2),
            new("UniPlus:Classificacao", 3),
            new("Relational:TableName", "pedidos"),
        ];

        IEnumerable<IAnnotation> restantes = gerador.FilterIgnoredAnnotations(anotacoes);

        restantes.Select(a => a.Name).Should().Equal("Relational:TableName");
    }

    [Fact(DisplayName = "A exceção do gerador do provider chega sem o invólucro da reflexão")]
    public void ExcecaoChegaDesembrulhada()
    {
        IAnnotationCodeGenerator provider = Substitute.For<IAnnotationCodeGenerator>();
        provider.FilterIgnoredAnnotations(Arg.Any<IEnumerable<IAnnotation>>()).Returns(_ => throw new InvalidOperationException("falha do provider"));
        IAnnotationCodeGenerator gerador = Decorado(provider);

        Action filtrar = () => gerador.FilterIgnoredAnnotations([]);

        filtrar.Should().Throw<InvalidOperationException>().WithMessage("falha do provider");
    }

    [Fact(DisplayName = "O registro do provider por instância também é decorado")]
    public void RegistroPorInstanciaEDecorado()
    {
        IAnnotationCodeGenerator gerador = Decorado(Provider());

        gerador.FilterIgnoredAnnotations([new Annotation("UniPlus:Classificacao", 1)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem registro do provider o filtro falha com mensagem clara")]
    public void SemRegistroFalha()
    {
        ServiceCollection services = new();

        Action registrar = () => services.ExcluirClassificacaoDoSnapshot();

        registrar.Should().Throw<InvalidOperationException>().WithMessage("*IAnnotationCodeGenerator*");
    }
}
