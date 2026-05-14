namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

public sealed class AreaDeInteresseBindingTests
{
    private static readonly AreaCodigo Ceps = AreaCodigo.From("CEPS").Value!;
    private static readonly DateTimeOffset ValidoDe = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_DeveAbrirVinculoVigente()
    {
        AreaDeInteresseBinding<EntidadeAreaScopedFake> binding =
            AreaDeInteresseBinding<EntidadeAreaScopedFake>.Criar(Guid.CreateVersion7(), Ceps, ValidoDe, "sub-1");

        binding.AreaCodigo.Should().Be(Ceps);
        binding.ValidoDe.Should().Be(ValidoDe);
        binding.ValidoAte.Should().BeNull("um vínculo recém-criado é vigente");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_DadoAdicionadoPorVazio_DeveLancar(string adicionadoPor)
    {
        Action criar = () => AreaDeInteresseBinding<EntidadeAreaScopedFake>.Criar(
            Guid.CreateVersion7(), Ceps, ValidoDe, adicionadoPor);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Encerrar_DadoValidoAtePosterior_DeveFecharAJanela()
    {
        AreaDeInteresseBinding<EntidadeAreaScopedFake> binding =
            AreaDeInteresseBinding<EntidadeAreaScopedFake>.Criar(Guid.CreateVersion7(), Ceps, ValidoDe, "sub-1");
        DateTimeOffset validoAte = ValidoDe.AddDays(30);

        binding.Encerrar(validoAte);

        binding.ValidoAte.Should().Be(validoAte);
    }

    [Theory]
    [InlineData(0, "janela vazia (validoAte == validoDe)")]
    [InlineData(-1, "janela invertida (validoAte < validoDe)")]
    public void Encerrar_DadoValidoAteNaoPosterior_DeveLancar(int diasOffset, string razao)
    {
        AreaDeInteresseBinding<EntidadeAreaScopedFake> binding =
            AreaDeInteresseBinding<EntidadeAreaScopedFake>.Criar(Guid.CreateVersion7(), Ceps, ValidoDe, "sub-1");

        Action encerrar = () => binding.Encerrar(ValidoDe.AddDays(diasOffset));

        encerrar.Should().Throw<ArgumentException>(razao);
        binding.ValidoAte.Should().BeNull("a tentativa inválida não altera o estado");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Usada apenas como argumento de tipo genérico de AreaDeInteresseBinding<T>.")]
    private sealed class EntidadeAreaScopedFake : IAreaScopedEntity
    {
        public AreaCodigo? Proprietario => null;
    }
}
