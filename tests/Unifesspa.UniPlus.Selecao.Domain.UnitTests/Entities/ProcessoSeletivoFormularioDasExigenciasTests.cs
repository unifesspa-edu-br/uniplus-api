namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
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
    [Fact(DisplayName = "A exigência de um formulário que o processo ainda não tem é aceita e guardada na gravação")]
    public void DefinirDocumentosExigidos_FormularioInexistente_Aceita()
    {
        FaseCronograma fase = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, Exigencia(fase.Id, papel: PapelDoFormularioDeTeste.Inscricao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle();
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
            ("regrasDeComprovacao[0].documento.momento.formularioCodigo", FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase),
            ("regrasDeComprovacao[2].documento.momento.formularioCodigo", FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase));
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

    [Fact(DisplayName = "A exigência declarada antes do formulário é recusada na publicação e no painel até o formulário da fase ser criado")]
    public void PendenciaDoFormularioDasExigencias_FormularioCriadoDepois_RecusaAteCriar()
    {
        FaseCronograma fase = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(fase);
        DefinirRegrasDeDocumento(processo, Exigencia(fase.Id, papel: PapelDoFormularioDeTeste.Inscricao)).IsSuccess.Should().BeTrue();

        PendenciasDasExigencias.PendenciaDoFormularioDasExigencias(processo.Declaracao())!.Code
            .Should().Be(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(static item => item.Codigo == "exigencia_no_formulario_da_sua_fase").Ok.Should().BeFalse();

        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);

        PendenciasDasExigencias.PendenciaDoFormularioDasExigencias(processo.Declaracao()).Should().BeNull();
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(static item => item.Codigo == "exigencia_no_formulario_da_sua_fase").Ok.Should().BeTrue();
    }
}
