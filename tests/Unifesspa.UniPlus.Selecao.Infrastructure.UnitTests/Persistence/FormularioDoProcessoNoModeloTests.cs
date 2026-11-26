namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// O código do formulário como chave dentro do processo, e o vínculo obrigatório de cada dado, grupo e termo com o
/// formulário que o exige. Não abre conexão: só o modelo.
/// </summary>
public sealed class FormularioDoProcessoNoModeloTests
{
    [Fact(DisplayName = "O código do formulário é chave alternativa dentro do processo, com nome próprio")]
    public void Formulario_CodigoDentroDoProcesso_EChaveAlternativa()
    {
        IKey chave = ModeloDaSelecao.Entidade(typeof(FormularioProcesso)).GetKeys().Single(static k => !k.IsPrimaryKey());

        chave.Properties.Select(static p => p.Name).Should().Equal(nameof(FormularioProcesso.ProcessoSeletivoId), nameof(FormularioProcesso.Codigo));
        chave.GetName().Should().Be("ux_formularios_processo_processo_codigo");
    }

    [Theory(DisplayName = "Dado, grupo e termo pertencem a um formulário e saem com ele")]
    [InlineData(typeof(FatoColetado))]
    [InlineData(typeof(GrupoColetado))]
    [InlineData(typeof(TermoExigidoFormulario))]
    public void Item_Formulario_EObrigatorioEEmCascata(Type entidade)
    {
        IEntityType tipo = ModeloDaSelecao.Entidade(entidade);

        tipo.FindProperty(nameof(FatoColetado.FormularioId))!.IsNullable.Should().BeFalse();
        IForeignKey chave = tipo.GetForeignKeys().Single(static f => f.PrincipalEntityType.ClrType == typeof(FormularioProcesso));
        chave.Properties.Select(static p => p.Name).Should().Equal(nameof(FatoColetado.FormularioId));
        chave.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact(DisplayName = "O código do termo é único dentro do formulário, e a ordem também")]
    public void Termo_CodigoEOrdem_SaoUnicosNoFormulario()
    {
        IEnumerable<IIndex> unicos = ModeloDaSelecao.Entidade(typeof(TermoExigidoFormulario)).GetIndexes().Where(static i => i.IsUnique);

        unicos.Select(static i => i.GetDatabaseName()).Should().BeEquivalentTo(
            ["ux_termos_exigidos_formulario_formulario_codigo", "ux_termos_exigidos_formulario_formulario_ordem"]);
        unicos.Should().OnlyContain(static i => i.Properties[0].Name == nameof(TermoExigidoFormulario.FormularioId));
    }

    [Fact(DisplayName = "A ordem do dado é única dentro do formulário, fora dos grupos")]
    public void Dado_Ordem_EUnicaNoFormularioForaDosGrupos()
    {
        IIndex indice = ModeloDaSelecao.Entidade(typeof(FatoColetado)).GetIndexes().Single(static i => i.GetDatabaseName() == "ux_dados_dos_formularios_formulario_ordem");

        indice.IsUnique.Should().BeTrue();
        indice.Properties.Select(static p => p.Name).Should().Equal(nameof(FatoColetado.FormularioId), nameof(FatoColetado.Ordem));
        indice.GetFilter().Should().Be("grupo_coletado_id IS NULL");
    }
}
