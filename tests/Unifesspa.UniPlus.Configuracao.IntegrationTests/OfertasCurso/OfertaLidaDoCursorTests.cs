namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// A remontagem da oferta de referência a partir das partes do cursor: o código de programa e de formato conhecido
/// volta ao valor do enum, e o desconhecido é defeito do cursor, nunca um valor padrão.
/// </summary>
public sealed class OfertaLidaDoCursorTests
{
    private static readonly IReadOnlyList<SortField> Campos =
    [
        new(CamposOrdenacaoOfertaCurso.ProgramaDeOferta, SortDirection.Ascending),
        new(CamposOrdenacaoOfertaCurso.FormatoPedagogico, SortDirection.Descending),
    ];

    private static readonly IReadOnlyList<SortField> CamposDosRegimes =
    [
        new(CamposOrdenacaoOfertaCurso.RegimeDeFuncionamento, SortDirection.Ascending),
        new(CamposOrdenacaoOfertaCurso.RegimeDeTurno, SortDirection.Descending),
    ];

    [Theory(DisplayName = "Os códigos conhecidos do cursor voltam ao programa e ao formato")]
    [InlineData("REGULAR", "PRESENCIAL", ProgramaDeOferta.Regular, FormatoPedagogico.Presencial)]
    [InlineData("FORMA_PARA", "SEMIPRESENCIAL", ProgramaDeOferta.FormaPara, FormatoPedagogico.Semipresencial)]
    [InlineData("CONVENIO_OUTRO", "EAD", ProgramaDeOferta.OutroConvenio, FormatoPedagogico.Ead)]
    public void Remontar_CodigosConhecidos_VoltamAoValor(
        string programa, string formato, ProgramaDeOferta programaEsperado, FormatoPedagogico formatoEsperado)
    {
        Guid id = Guid.CreateVersion7();

        OfertaCursoOrdenada oferta = Remontar(Campos, [programa, formato], id);

        oferta.Id.Should().Be(id);
        oferta.ProgramaDeOferta.Should().Be(programaEsperado);
        oferta.FormatoPedagogico.Should().Be(formatoEsperado);
    }

    [Theory(DisplayName = "Código desconhecido, em outra caixa ou com espaço lança ArgumentException")]
    [InlineData("PROUNI", "EAD")]
    [InlineData("regular", "EAD")]
    [InlineData("REGULAR", "HIBRIDO")]
    [InlineData("REGULAR", " EAD")]
    [InlineData("REGULAR", "")]
    public void Remontar_CodigoDesconhecido_LancaArgumentException(string programa, string formato)
    {
        Action act = () => Remontar(Campos, [programa, formato], Guid.CreateVersion7());

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Os códigos conhecidos do cursor voltam aos regimes de funcionamento e de turno")]
    [InlineData("INTENSIVO", "INTEGRAL", RegimeDeFuncionamento.Intensivo, RegimeDeTurno.Integral)]
    [InlineData("EXTENSIVO", "REGULAR", RegimeDeFuncionamento.Extensivo, RegimeDeTurno.Regular)]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "INTEGRAL", RegimeDeFuncionamento.AlternanciaPedagogica, RegimeDeTurno.Integral)]
    public void Remontar_RegimesConhecidos_VoltamAoValor(
        string funcionamento, string regime, RegimeDeFuncionamento funcionamentoEsperado, RegimeDeTurno regimeEsperado)
    {
        OfertaCursoOrdenada oferta = Remontar(CamposDosRegimes, [funcionamento, regime], Guid.CreateVersion7());

        oferta.RegimeDeFuncionamento.Should().Be(funcionamentoEsperado);
        oferta.RegimeDeTurno.Should().Be(regimeEsperado);
    }

    [Theory(DisplayName = "Regime desconhecido, em outra caixa, com espaço ou vazio lança ArgumentException")]
    [InlineData("SEMI_INTENSIVO", "INTEGRAL")]
    [InlineData("intensivo", "INTEGRAL")]
    [InlineData(" INTENSIVO", "INTEGRAL")]
    [InlineData("", "INTEGRAL")]
    [InlineData("INTENSIVO", "PARCIAL")]
    [InlineData("INTENSIVO", "regular")]
    [InlineData("INTENSIVO", "REGULAR ")]
    [InlineData("INTENSIVO", "")]
    public void Remontar_RegimeDesconhecido_LancaArgumentException(string funcionamento, string regime)
    {
        Action act = () => Remontar(CamposDosRegimes, [funcionamento, regime], Guid.CreateVersion7());

        act.Should().Throw<ArgumentException>();
    }

    private static OfertaCursoOrdenada Remontar(IReadOnlyList<SortField> campos, string[] partes, Guid id)
    {
        KeysetSort<OfertaCursoOrdenada> ordenacao = OrdenacaoDeCursos.DeOfertas(campos, []);
        PropertyInfo propriedade = typeof(KeysetSort<OfertaCursoOrdenada>)
            .GetProperty("BuildAnchor", BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().NotBeNull().And.Subject.As<PropertyInfo>();
        Func<IReadOnlyList<string>, Guid, object> montar = propriedade.GetValue(ordenacao)
            .Should().BeAssignableTo<Func<IReadOnlyList<string>, Guid, object>>().Which;

        return montar(partes, id).Should().BeOfType<OfertaCursoOrdenada>().Which;
    }
}
