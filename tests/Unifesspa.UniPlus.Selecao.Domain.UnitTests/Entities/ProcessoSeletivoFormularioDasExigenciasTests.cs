namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// A exigência de documento cita o formulário que a entrega pelo código, e só vale num formulário que o processo tem,
/// ligado à mesma fase da exigência, obrigatório ou facultativo (ADR-0171). As recusas de exigências diferentes saem juntas.
/// </summary>
public sealed class ProcessoSeletivoFormularioDasExigenciasTests
{
    private static (ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Isencao) ProcessoComOsDoisFormularios()
    {
        FaseCronograma inscricao = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: true);
        FaseCronograma isencao = FaseQueResponde(2, "SOLICITACAO_ISENCAO", inscricao: true, isencao: true);
        ProcessoSeletivo processo = ProcessoComFases(inscricao, isencao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        processo.DefinirFormulario(
                FormularioDeTeste.DeclaradoNoPapel(PapelDoFormularioDeTeste.IsencaoTaxa, isencao.Id), null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.IsencaoTaxa))
            .IsSuccess.Should().BeTrue();

        return (processo, inscricao, isencao);
    }

    [Fact(DisplayName = "A exigência de um formulário que o processo não tem é recusada")]
    public void DefinirDocumentosExigidos_FormularioInexistente_Recusa()
    {
        FaseCronograma fase = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, Exigencia(fase.Id, papel: PapelDoFormularioDeTeste.Inscricao));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente);
    }

    [Fact(DisplayName = "A exigência de um formulário ligado a outra fase é recusada")]
    public void DefinirDocumentosExigidos_FormularioDeOutraFase_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _) = ProcessoComOsDoisFormularios();

        Result resultado = DefinirRegrasDeDocumento(processo, Exigencia(inscricao.Id, papel: PapelDoFormularioDeTeste.IsencaoTaxa));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }

    [Fact(DisplayName = "A exigência de um formulário facultativo, na fase dele, é aceita")]
    public void DefinirDocumentosExigidos_FormularioFacultativoNaFaseDele_Aceita()
    {
        (ProcessoSeletivo processo, _, FaseCronograma isencao) = ProcessoComOsDoisFormularios();

        Result resultado = DefinirRegrasDeDocumento(processo, Exigencia(isencao.Id, papel: PapelDoFormularioDeTeste.IsencaoTaxa));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "As recusas de exigências diferentes saem juntas, cada uma na posição da sua exigência")]
    public void DefinirDocumentosExigidos_ExigenciasRecusadas_AcumulamAsRecusas()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, FaseCronograma isencao) = ProcessoComOsDoisFormularios();

        Result resultado = DefinirRegrasDeDocumento(
            processo,
            Exigencia(inscricao.Id, papel: PapelDoFormularioDeTeste.IsencaoTaxa),
            Exigencia(isencao.Id, papel: PapelDoFormularioDeTeste.IsencaoTaxa),
            Exigencia(isencao.Id, papel: PapelDoFormularioDeTeste.Inscricao));

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("exigencias[0].codigoDoFormulario", FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase),
            ("exigencias[2].codigoDoFormulario", FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase));
    }

    [Fact(DisplayName = "Na publicação, a exigência cujo formulário passou para outra fase é recusada")]
    public void PendenciaDoFormularioDasExigencias_FormularioMudouDeFase_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, FaseCronograma isencao) = ProcessoComOsDoisFormularios();
        ExigirDocumentos(processo, Exigencia(isencao.Id, papel: PapelDoFormularioDeTeste.IsencaoTaxa));
        PendenciasDasExigencias.PendenciaDoFormularioDasExigencias(processo.Declaracao()).Should().BeNull();

        processo.DefinirFormulario(
                FormularioDeTeste.DeclaradoNoPapel(PapelDoFormularioDeTeste.IsencaoTaxa, inscricao.Id), null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.IsencaoTaxa))
            .IsSuccess.Should().BeTrue();

        PendenciasDasExigencias.PendenciaDoFormularioDasExigencias(processo.Declaracao())!.Code
            .Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase);
    }
}
