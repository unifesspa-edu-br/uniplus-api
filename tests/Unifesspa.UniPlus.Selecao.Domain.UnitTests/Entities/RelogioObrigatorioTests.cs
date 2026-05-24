namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Pin do contrato da ADR-0068: métodos de domínio que leem o relógio exigem
/// <see cref="System.TimeProvider"/> — passar <c>null</c> lança
/// <see cref="System.ArgumentNullException"/>. Cobre os guards introduzidos
/// junto da convenção (sem default de relógio).
/// </summary>
public sealed class RelogioObrigatorioTests
{
    private static NumeroEdital Numero() => NumeroEdital.Criar(numero: 1, ano: 2026).Value!;

    [Fact(DisplayName = "Edital.Publicar exige TimeProvider não-nulo")]
    public void EditalPublicar_ClockNulo_Lanca()
    {
        Edital edital = Edital.Criar(Numero(), "Edital teste");

        Action act = () => edital.Publicar(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Inscricao.Criar exige TimeProvider não-nulo")]
    public void InscricaoCriar_ClockNulo_Lanca()
    {
        Action act = () => Inscricao.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModalidadeConcorrencia.AC,
            "CURSO01",
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Inscricao.Confirmar exige TimeProvider não-nulo")]
    public void InscricaoConfirmar_ClockNulo_Lanca()
    {
        Inscricao inscricao = Inscricao.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModalidadeConcorrencia.AC,
            "CURSO01",
            TimeProvider.System).Value!;

        Action act = () => inscricao.Confirmar(null!);

        act.Should().Throw<ArgumentNullException>();
    }

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
