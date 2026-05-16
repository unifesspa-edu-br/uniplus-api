namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura da factory <see cref="Edital.Criar"/> após a Story #454 ter
/// substituído o enum <c>TipoProcesso</c> pela FK preparatória
/// <c>TipoEditalId</c> (Guid?). Garante que a invariante "opcional ou
/// FK válida" é honrada pelo domínio, mesmo quando callers internos
/// (handlers, seeds, fixtures) bypassam o validator de borda.
/// </summary>
public sealed class EditalCriarTests
{
    private static NumeroEdital ValidNumeroEdital()
    {
        Result<NumeroEdital> result = NumeroEdital.Criar(numero: 1, ano: 2026);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    [Fact(DisplayName = "Edital.Criar sem TipoEditalId mantem o campo nulo e nasce em Rascunho")]
    public void Criar_SemTipoEditalId_FicaNulo()
    {
        Edital edital = Edital.Criar(ValidNumeroEdital(), "Edital teste");

        edital.TipoEditalId.Should().BeNull();
        edital.Status.Should().Be(StatusEdital.Rascunho);
    }

    [Fact(DisplayName = "Edital.Criar com TipoEditalId valido (Guid v7) preserva o valor")]
    public void Criar_ComTipoEditalIdValido_PreservaValor()
    {
        Guid tipoEditalId = Guid.CreateVersion7();

        Edital edital = Edital.Criar(ValidNumeroEdital(), "Edital teste", tipoEditalId);

        edital.TipoEditalId.Should().Be(tipoEditalId);
    }

    [Fact(DisplayName = "Edital.Criar com TipoEditalId = Guid.Empty lanca ArgumentException")]
    public void Criar_ComTipoEditalIdEmpty_Lanca()
    {
        Action act = () => Edital.Criar(ValidNumeroEdital(), "Edital teste", Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tipoEditalId")
            .WithMessage("*Guid vazio*");
    }
}
