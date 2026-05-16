namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Cobertura da factory <see cref="Etapa.Criar"/> após a Story #454 ter
/// substituído o enum <c>TipoEtapa</c> pela FK preparatória
/// <c>TipoEtapaId</c> (Guid?). Simétrica a
/// <see cref="EditalCriarTests"/> — guarda contra <c>Guid.Empty</c>.
/// </summary>
public sealed class EtapaCriarTests
{
    [Fact(DisplayName = "Etapa.Criar sem TipoEtapaId mantem o campo nulo")]
    public void Criar_SemTipoEtapaId_FicaNulo()
    {
        Etapa etapa = Etapa.Criar(
            editalId: Guid.CreateVersion7(),
            nome: "Prova objetiva",
            peso: 1.0m,
            ordem: 1);

        etapa.TipoEtapaId.Should().BeNull();
    }

    [Fact(DisplayName = "Etapa.Criar com TipoEtapaId valido preserva o valor")]
    public void Criar_ComTipoEtapaIdValido_PreservaValor()
    {
        Guid tipoEtapaId = Guid.CreateVersion7();

        Etapa etapa = Etapa.Criar(
            editalId: Guid.CreateVersion7(),
            nome: "Prova objetiva",
            peso: 1.0m,
            ordem: 1,
            tipoEtapaId: tipoEtapaId);

        etapa.TipoEtapaId.Should().Be(tipoEtapaId);
    }

    [Fact(DisplayName = "Etapa.Criar com TipoEtapaId = Guid.Empty lanca ArgumentException")]
    public void Criar_ComTipoEtapaIdEmpty_Lanca()
    {
        Action act = () => Etapa.Criar(
            editalId: Guid.CreateVersion7(),
            nome: "Prova objetiva",
            peso: 1.0m,
            ordem: 1,
            tipoEtapaId: Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tipoEtapaId")
            .WithMessage("*Guid vazio*");
    }
}
