namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// Como o modelo guarda o identificador legível: todo processo gravado o tem, então a coluna é obrigatória e o índice único
/// vale para todas as linhas. Não abre conexão: só o modelo.
/// </summary>
public sealed class IdentificadorLegivelNoModeloTests
{
    [Fact(DisplayName = "A coluna do identificador legível é obrigatória")]
    public void Coluna_NaoAceitaNulo()
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(typeof(ProcessoSeletivo), nameof(ProcessoSeletivo.IdentificadorLegivel));

        propriedade.IsNullable.Should().BeFalse();
    }

    [Fact(DisplayName = "O comentário da coluna não diz que o identificador pode estar ausente")]
    public void Coluna_ComentarioNaoAdmiteAusencia()
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(typeof(ProcessoSeletivo), nameof(ProcessoSeletivo.IdentificadorLegivel));

        propriedade.GetComment().Should().NotBeNullOrWhiteSpace()
            .And.NotContain("Ausência")
            .And.NotContain("ainda não declarado");
    }

    [Fact(DisplayName = "O índice único do identificador legível vale para todas as linhas, sem filtro")]
    public void Indice_UnicoSemFiltro()
    {
        IIndex indice = ModeloDaSelecao.Entidade(typeof(ProcessoSeletivo)).GetIndexes()
            .Single(static i => i.GetDatabaseName() == "ux_processos_seletivos_identificador_legivel");

        indice.Properties.Select(static p => p.Name).Should().Equal(nameof(ProcessoSeletivo.IdentificadorLegivel));
        indice.IsUnique.Should().BeTrue();
        indice.GetFilter().Should().BeNull();
    }
}
