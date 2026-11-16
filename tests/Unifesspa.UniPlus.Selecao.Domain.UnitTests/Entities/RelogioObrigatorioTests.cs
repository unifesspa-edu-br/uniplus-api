namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Pin do contrato da ADR-0068: métodos de domínio que leem o relógio exigem
/// <see cref="System.TimeProvider"/> — passar <c>null</c> lança
/// <see cref="System.ArgumentNullException"/>. Cobre os guards introduzidos
/// junto da convenção (sem default de relógio).
/// </summary>
public sealed class RelogioObrigatorioTests
{
    [Fact(DisplayName = "DocumentoAPublicar.IniciarPendente exige TimeProvider não-nulo")]
    public void DocumentoAPublicarIniciarPendente_ClockNulo_Lanca()
    {
        Action act = () => DocumentoAPublicar.IniciarPendente(
            Guid.CreateVersion7(), FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, clock: null!, TimeSpan.FromMinutes(15));

        act.Should().Throw<ArgumentNullException>();
    }
}
