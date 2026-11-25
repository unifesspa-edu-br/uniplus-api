namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ObjetoDeValorGravadoTests
{
    [Fact(DisplayName = "A leitura devolve a mesma instância de um valor válido")]
    public void Reidratar_ClasseValida_DevolveAMesmaInstancia()
    {
        Result<Percentual> criacao = Percentual.Criar(12.34m);

        Percentual valor = ObjetoDeValorGravado.Reidratar(criacao, nameof(Percentual));

        valor.Should().BeSameAs(criacao.Value);
    }

    [Fact(DisplayName = "A leitura devolve um tipo por valor válido")]
    public void Reidratar_TipoPorValorValido_DevolveOValor()
    {
        int valor = ObjetoDeValorGravado.Reidratar(Result<int>.Success(7), "Número");

        valor.Should().Be(7);
    }

    [Fact(DisplayName = "A leitura inválida informa o nome e o código sem a descrição do erro")]
    public void Reidratar_Falha_LancaComNomeECodigo()
    {
        Result<int> criacao = Result<int>.Failure(new DomainError("Teste.Invalido", "Descrição privada"));

        FluentActions.Invoking(() => ObjetoDeValorGravado.Reidratar(criacao, "Número"))
            .Should().Throw<InvalidOperationException>().Which.Message.Should().Be(
                "Dado inválido no banco ao reidratar Número: Teste.Invalido. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
    }
}
