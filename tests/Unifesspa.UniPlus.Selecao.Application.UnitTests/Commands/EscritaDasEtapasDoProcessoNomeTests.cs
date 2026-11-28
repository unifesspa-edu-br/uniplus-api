namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O nome da etapa omitido é recusado pela forma da etapa, junto das demais recusas do pedido, e o período invertido
/// vem junto quando a forma de alguma etapa já foi recusada.
/// </summary>
public sealed class EscritaDasEtapasDoProcessoNomeTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimAntesDoInicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static EtapaProcessoInput Etapa(string? nome, int ordem = 1) => new(
        nome, CaraterEtapa.Classificatoria.Codigo(), Guid.CreateVersion7(), 1m, null, ordem,
        Produtos: [], Bancas: [], Recursos: []);

    private static (string?, string)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Theory(DisplayName = "Nome ausente, nulo, vazio ou em branco é recusado apontando o nome da etapa")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Conferir_NomeOmitidoOuEmBranco_RecusaONome(string? nome)
    {
        Result resultado = EscritaDasEtapasDoProcesso.Conferir([Etapa(nome)]);

        Recusas(resultado).Should().Equal(("etapas[0].nome", "EtapaProcesso.NomeObrigatorio"));
    }

    [Fact(DisplayName = "Nome informado e válido é aceito")]
    public void Conferir_NomeValido_Aceita() =>
        EscritaDasEtapasDoProcesso.Conferir([Etapa("Prova Objetiva")]).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Nome ausente junto de ordem inválida devolve as duas recusas, na ordem da etapa")]
    public void Conferir_NomeAusenteEOrdemInvalida_RecusaAsDuas()
    {
        Result resultado = EscritaDasEtapasDoProcesso.Conferir([Etapa(null, ordem: 0)]);

        Recusas(resultado).Should().Equal(
            ("etapas[0].nome", "EtapaProcesso.NomeObrigatorio"),
            ("etapas[0].ordem", "EtapaProcesso.OrdemInvalida"));
    }

    [Fact(DisplayName = "Nome ausente junto de período invertido devolve as duas recusas na mesma resposta")]
    public void Conferir_NomeAusenteEPeriodoInvertido_RecusaAsDuas()
    {
        EtapaProcessoInput etapa = Etapa(null) with { Inicio = Inicio, Fim = FimAntesDoInicio };

        Result resultado = EscritaDasEtapasDoProcesso.Conferir([etapa]);

        Recusas(resultado).Should().Equal(
            ("etapas[0].nome", "EtapaProcesso.NomeObrigatorio"),
            ("etapas[0].fim", "EtapaProcesso.JanelaInvertida"));
    }

    [Fact(DisplayName = "O período invertido de uma etapa vem junto do nome ausente de outra")]
    public void Conferir_NomeAusenteNumaEtapaEPeriodoInvertidoNaOutra_RecusaAsDuas()
    {
        EtapaProcessoInput invertida = Etapa("Redação", ordem: 2) with { Inicio = Inicio, Fim = FimAntesDoInicio };

        Result resultado = EscritaDasEtapasDoProcesso.Conferir([Etapa(null), invertida]);

        Recusas(resultado).Should().Equal(
            ("etapas[0].nome", "EtapaProcesso.NomeObrigatorio"),
            ("etapas[1].fim", "EtapaProcesso.JanelaInvertida"));
    }

    [Fact(DisplayName = "Período invertido sozinho não é recusado aqui: a conferência do tipo o soma às suas recusas")]
    public void Conferir_SoPeriodoInvertido_Aceita()
    {
        EtapaProcessoInput etapa = Etapa("Prova Objetiva") with { Inicio = Inicio, Fim = FimAntesDoInicio };

        EscritaDasEtapasDoProcesso.Conferir([etapa]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Período com um só extremo não é invertido")]
    public void Conferir_NomeAusenteEPeriodoSemFim_NaoRecusaPeriodo()
    {
        EtapaProcessoInput etapa = Etapa(null) with { Inicio = Inicio };

        Recusas(EscritaDasEtapasDoProcesso.Conferir([etapa])).Should().Equal(("etapas[0].nome", "EtapaProcesso.NomeObrigatorio"));
    }
}
