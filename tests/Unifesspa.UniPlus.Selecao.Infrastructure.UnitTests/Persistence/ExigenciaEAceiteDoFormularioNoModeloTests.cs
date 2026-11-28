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

    [Fact(DisplayName = "A exigência de documento cita o formulário só pelo código, sem chave estrangeira nem índice próprio")]
    public void Exigencia_Formulario_SemChaveEstrangeiraNemIndice()
    {
        IEntityType exigencia = ModeloDaSelecao.Entidade(typeof(DocumentoExigido));

        exigencia.GetForeignKeys().Should().NotContain(static f => f.PrincipalEntityType.ClrType == typeof(FormularioProcesso));
        exigencia.GetIndexes().Should().NotContain(static i => i.Properties.Any(static p => p.Name == nameof(DocumentoExigido.FormularioCodigo)));
    }

    [Fact(DisplayName = "A exigência de documento só admite o código do formulário na forma do código")]
    public void Exigencia_CodigoDoFormulario_TemRestricaoDeFormato() =>
        ModeloDaSelecao.SqlDaRestricao(typeof(DocumentoExigido), "ck_documentos_exigidos_formulario_codigo_formato")
            .Should().StartWith("formulario_codigo ~ '");

    [Fact(DisplayName = "A coluna do formulário da exigência de documento tem comentário")]
    public void Exigencia_ColunaDoFormulario_TemComentario() =>
        ModeloDaSelecao.Propriedade(typeof(DocumentoExigido), nameof(DocumentoExigido.FormularioCodigo))
            .GetComment().Should().NotBeNullOrWhiteSpace();

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
