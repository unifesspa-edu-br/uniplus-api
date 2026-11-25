namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CpfDaInscricaoTests
{
    [Fact]
    public void Revalidar_CpfInvalido_FalhaSemExporValor()
    {
        const string valor = "00000000000";
        Action revalidar = () => CpfDaInscricao.Revalidar(valor);

        string mensagem = revalidar.Should().Throw<InvalidOperationException>().Which.Message;
        mensagem.Should().Be("Dado inválido no banco ao reidratar Cpf: Cpf.Invalido. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        mensagem.Should().NotContain(valor);
    }

    [Fact]
    public void Revalidar_CpfValido_RetornaCpf()
    {
        string valor = PersonasDeTeste.KevinCarvalho.Valor;

        Cpf cpf = CpfDaInscricao.Revalidar(valor);

        cpf.Valor.Should().Be(valor);
    }
}
