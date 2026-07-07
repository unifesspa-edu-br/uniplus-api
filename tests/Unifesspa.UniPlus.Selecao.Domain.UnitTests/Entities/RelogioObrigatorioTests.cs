namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Pin do contrato da ADR-0068: métodos de domínio que leem o relógio exigem
/// <see cref="System.TimeProvider"/> — passar <c>null</c> lança
/// <see cref="System.ArgumentNullException"/>. Cobre os guards introduzidos
/// junto da convenção (sem default de relógio).
/// </summary>
public sealed class RelogioObrigatorioTests
{
    [Fact(DisplayName = "ObrigatoriedadeLegal.Criar (conveniência) exige TimeProvider não-nulo")]
    public void ObrigatoriedadeLegalCriar_ClockNulo_Lanca()
    {
        PredicadoObrigatoriedade predicado = new EtapaObrigatoria("ProvaObjetiva");

        Action act = () => ObrigatoriedadeLegal.Criar(
            regraCodigo: "REGRA",
            predicado: predicado,
            baseLegal: "Lei 12.711/2012",
            descricaoHumana: "desc",
            portariaInternaCodigo: null,
            clock: null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
