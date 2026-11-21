namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A semente exige do domínio o que ela declara: o que o domínio recusa interrompe a preparação do banco
/// com uma exceção que nomeia a semente e o item, em vez de gravar linha que a API não aceita.
/// </summary>
public sealed class DeclaracaoDaSementeTests
{
    private static readonly DeclaracaoDaSemente Declaracao = new(
        "de teste", "semente-de-teste", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private static readonly DomainError Recusa = new("teste.recusa", "O item não é aceito.");

    [Fact(DisplayName = "Exigir devolve o valor do resultado que deu certo")]
    public void Exigir_ResultadoComValor_DevolveOValor()
    {
        Declaracao.Exigir(Result<string>.Success("valor"), "item").Should().Be("valor");
    }

    [Fact(DisplayName = "Exigir de um resultado com valor recusado lança nomeando a semente, o item e a causa")]
    public void Exigir_ResultadoComValorRecusado_LancaNomeandoSementeItemECausa()
    {
        Action exigir = () => Declaracao.Exigir(Result<string>.Failure(Recusa), "modelo X");

        exigir.Should().Throw<InvalidOperationException>()
            .WithMessage("A semente de teste tem modelo X que o domínio recusa: : O item não é aceito.");
    }

    [Fact(DisplayName = "Exigir de um resultado sem valor recusado lança nomeando a semente, o item e cada causa")]
    public void Exigir_ResultadoSemValorRecusado_LancaNomeandoSementeItemECadaCausa()
    {
        Result recusado = Result.ValidationFailure(
            [new FieldError("nome", Recusa), new FieldError("texto", new DomainError("teste.outra", "Falta o texto."))]);
        Action exigir = () => Declaracao.Exigir(recusado, "termo T");

        exigir.Should().Throw<InvalidOperationException>()
            .WithMessage("A semente de teste tem termo T que o domínio recusa: nome: O item não é aceito.; texto: Falta o texto.");
    }

    [Fact(DisplayName = "Exigir de um resultado sem valor que deu certo não lança")]
    public void Exigir_ResultadoSemValorQueDeuCerto_NaoLanca()
    {
        Action exigir = () => Declaracao.Exigir(Result.Success(), "item");

        exigir.Should().NotThrow();
    }

    [Fact(DisplayName = "O termo que o domínio recusa interrompe a gravação com a recusa nomeando a semente e o termo")]
    public void ComandosDoTermo_TermoRecusado_LancaNomeandoSementeETermo()
    {
        TermoDaSemente semente = new(Guid.NewGuid(), Guid.NewGuid(), "", "Texto do termo.", "Base legal.");
        Action gravar = () => _ = SqlDaSemente.ComandosDoTermo(semente, Declaracao).ToList();

        gravar.Should().Throw<InvalidOperationException>()
            .WithMessage("A semente de teste tem termo  que o domínio recusa:*");
    }
}
