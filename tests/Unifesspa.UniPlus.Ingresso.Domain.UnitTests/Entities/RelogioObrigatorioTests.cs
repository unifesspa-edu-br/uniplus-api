namespace Unifesspa.UniPlus.Ingresso.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
using Unifesspa.UniPlus.Ingresso.TestSupport.Cenarios;

/// <summary>
/// Pin do contrato da ADR-0068: métodos de domínio que leem o relógio exigem
/// <see cref="System.TimeProvider"/> — passar <c>null</c> lança
/// <see cref="System.ArgumentNullException"/>. Cobre os guards introduzidos
/// junto da convenção (sem default de relógio).
/// </summary>
public sealed class RelogioObrigatorioTests
{
    private static readonly TimeProvider Relogio = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    private static ProtocoloConvocacao Protocolo() => ProtocoloConvocacao.Gerar(1, 1, Relogio);

    private static Convocacao NovaConvocacao() => Convocacao.Criar(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Protocolo(),
        posicao: 1,
        ofertaCurso: new CopiaDaOfertaDeCursoDeTeste().Construir(),
        clock: Relogio);

    [Fact(DisplayName = "ProtocoloConvocacao.Gerar exige TimeProvider não-nulo")]
    public void ProtocoloGerar_ClockNulo_Lanca()
    {
        Action act = () => ProtocoloConvocacao.Gerar(1, 1, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Convocacao.Criar exige TimeProvider não-nulo")]
    public void ConvocacaoCriar_ClockNulo_Lanca()
    {
        Action act = () => Convocacao.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Protocolo(),
            posicao: 1,
            ofertaCurso: new CopiaDaOfertaDeCursoDeTeste().Construir(),
            clock: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Convocacao.Aceitar exige TimeProvider não-nulo")]
    public void ConvocacaoAceitar_ClockNulo_Lanca()
    {
        Convocacao convocacao = NovaConvocacao();

        Action act = () => convocacao.Aceitar(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Convocacao.Recusar exige TimeProvider não-nulo")]
    public void ConvocacaoRecusar_ClockNulo_Lanca()
    {
        Convocacao convocacao = NovaConvocacao();

        Action act = () => convocacao.Recusar(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Matricula.Efetivar exige TimeProvider não-nulo")]
    public void MatriculaEfetivar_ClockNulo_Lanca()
    {
        Matricula matricula = Matricula.Criar(Guid.NewGuid(), Guid.NewGuid(), new CopiaDaOfertaDeCursoDeTeste().Construir());

        Action act = () => matricula.Efetivar(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
