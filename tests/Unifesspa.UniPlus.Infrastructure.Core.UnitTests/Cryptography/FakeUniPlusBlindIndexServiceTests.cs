namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using AwesomeAssertions;

public sealed class FakeUniPlusBlindIndexServiceTests
{
    [Fact]
    public async Task ComputarAsync_MesmoValorEMesmaChave_DeveDevolverOMesmoIndice()
    {
        FakeUniPlusBlindIndexService sut = new();
        byte[] valor = "valor-opaco-um"u8.ToArray();

        byte[] indice1 = await sut.ComputarAsync("cpf", valor);
        byte[] indice2 = await sut.ComputarAsync("cpf", valor);

        indice1.Should().Equal(indice2);
    }

    [Fact]
    public async Task ComputarAsync_ChaveDiferente_DeveDevolverIndiceDiferente()
    {
        FakeUniPlusBlindIndexService sut = new();
        byte[] valor = "valor-opaco-um"u8.ToArray();

        byte[] indiceA = await sut.ComputarAsync("cpf", valor);
        byte[] indiceB = await sut.ComputarAsync("rg", valor);

        indiceA.Should().NotEqual(indiceB);
    }
}
