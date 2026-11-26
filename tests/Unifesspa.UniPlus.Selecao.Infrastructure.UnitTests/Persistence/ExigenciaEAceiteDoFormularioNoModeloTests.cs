namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// O formulário pelo código na exigência de documento e no aceite de termo: coluna obrigatória de até 60 caracteres,
/// vínculo com o formulário do mesmo processo e formato conferido no banco. Não abre conexão: só o modelo.
/// </summary>
public sealed class ExigenciaEAceiteDoFormularioNoModeloTests
{
    private const int TamanhoMaximoDoCodigoDoFormulario = 60;

    [Fact(DisplayName = "A exigência de documento guarda o código do formulário, obrigatório e de até 60 caracteres")]
    public void Exigencia_CodigoDoFormulario_EObrigatorioEDeAte60Caracteres()
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(typeof(DocumentoExigido), nameof(DocumentoExigido.FormularioCodigo));

        propriedade.IsNullable.Should().BeFalse();
        propriedade.GetMaxLength().Should().Be(TamanhoMaximoDoCodigoDoFormulario);
    }

    [Fact(DisplayName = "A exigência de documento liga o formulário do mesmo processo e não o apaga em cascata")]
    public void Exigencia_Formulario_ChaveComposta_SemCascata()
    {
        IForeignKey chave = ModeloDaSelecao.Entidade(typeof(DocumentoExigido)).GetForeignKeys()
            .Single(static f => f.PrincipalEntityType.ClrType == typeof(FormularioProcesso));

        chave.Properties.Select(static p => p.Name).Should().Equal(nameof(DocumentoExigido.ProcessoSeletivoId), nameof(DocumentoExigido.FormularioCodigo));
        chave.PrincipalKey.Properties.Select(static p => p.Name).Should().Equal(nameof(FormularioProcesso.ProcessoSeletivoId), nameof(FormularioProcesso.Codigo));
        chave.GetConstraintName().Should().Be("fk_documentos_exigidos_formulario");
        chave.DeleteBehavior.Should().Be(DeleteBehavior.NoAction);
    }

    [Fact(DisplayName = "A exigência de documento não guarda finalidade e comenta a coluna do formulário")]
    public void Exigencia_Modelo_SemFinalidadeEComComentario()
    {
        IEntityType exigencia = ModeloDaSelecao.Entidade(typeof(DocumentoExigido));

        exigencia.FindProperty("Finalidade").Should().BeNull();
        exigencia.FindProperty(nameof(DocumentoExigido.FormularioCodigo))!.GetComment().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "O aceite do termo guarda o código do formulário, obrigatório e de até 60 caracteres")]
    public void Aceite_CodigoDoFormulario_EObrigatorioEDeAte60Caracteres()
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(typeof(AceiteDaInscricaoRecord), nameof(AceiteDaInscricaoRecord.FormularioCodigo));

        propriedade.IsNullable.Should().BeFalse();
        propriedade.GetMaxLength().Should().Be(TamanhoMaximoDoCodigoDoFormulario);
    }

    [Fact(DisplayName = "O aceite do termo só admite o código do formulário na forma do código")]
    public void Aceite_CodigoDoFormulario_TemRestricaoDeFormato() =>
        ModeloDaSelecao.SqlDaRestricao(typeof(AceiteDaInscricaoRecord), "ck_aceites_da_inscricao_formulario_codigo_formato")
            .Should().StartWith("formulario_codigo ~ '");
}
