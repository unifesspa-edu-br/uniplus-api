namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioFormulariosPorFinalidade;

/// <summary>
/// O formulário é identificado pelo código: o que ele entrega, a ordem em que o documento o escreve e as seções dos itens
/// dele se conferem por ele, e não pela finalidade que a configuração antiga declarava (ADR-0171).
/// </summary>
public sealed class FormulariosPorCodigoNaConfiguracaoTests
{
    private static (ProcessoSeletivo Processo, FaseCronograma Fase) ProcessoComFormularioDaInscricao()
    {
        FaseCronograma fase = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: true);
        ProcessoSeletivo processo = ProcessoComFases(fase);
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Inscricao);
        return (processo, fase);
    }

    [Fact(DisplayName = "O formulário que ainda entrega exigência de documento não pode ser removido")]
    public void RemoverFormulario_QueEntregaExigencia_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComFormularioDaInscricao();
        processo.DefinirFormulario(FormularioDeTeste.DeclaradoNoPapel(FinalidadeFormulario.IsencaoTaxa, fase.Id), null, FormularioDeTeste.Paginas(FinalidadeFormulario.IsencaoTaxa))
            .IsSuccess.Should().BeTrue();
        Result exigido = DefinirRegrasDeDocumento(processo, Exigencia(fase.Id, finalidade: FinalidadeFormulario.IsencaoTaxa));
        exigido.IsSuccess.Should().BeTrue(string.Join("; ", exigido.Errors.Select(static e => e.Error.Message)));

        Result resultado = processo.RemoverFormulario(FinalidadeFormulario.IsencaoTaxa);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FormulariosDeclaradosErrorCodes.RemocaoDeFormularioComExigencia);
        processo.FormularioDe(FinalidadeFormulario.IsencaoTaxa).Should().NotBeNull("a recusa não muda o processo");
    }

    [Fact(DisplayName = "O formulário que não entrega exigência é removido")]
    public void RemoverFormulario_SemExigencia_Remove()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComFormularioDaInscricao();
        processo.DefinirFormulario(FormularioDeTeste.DeclaradoNoPapel(FinalidadeFormulario.Habilitacao, fase.Id), null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.RemoverFormulario(FinalidadeFormulario.Habilitacao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull();
    }

    [Fact(DisplayName = "Os campos saem na ordem do código do formulário que os coleta e, dentro dele, na ordem do campo")]
    public void Campos_DeDoisFormularios_OrdenaPeloCodigoDoFormularioEDepoisPelaOrdem()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComFormularioDaInscricao();
        processo.DefinirFormulario(FormularioDeTeste.DeclaradoNoPapel(FinalidadeFormulario.Habilitacao, fase.Id), null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("CERTIFICADO", 7), Item("DIPLOMA", 3)], FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        IReadOnlyList<FatoColetado> ordenados = [.. OrdemCanonicaDaConfiguracao.Campos([.. processo.Campos.Reverse()], processo.Formularios)];

        Guid habilitacao = processo.FormularioDe(FinalidadeFormulario.Habilitacao)!.Id;
        ordenados.Take(2).Select(static f => f.FatoCodigo).Should().Equal("DIPLOMA", "CERTIFICADO");
        ordenados.Take(2).Should().OnlyContain(f => f.FormularioId == habilitacao, "HABILITACAO vem antes de INSCRICAO na ordem ordinal");
        ordenados.Skip(2).Select(static f => f.Ordem).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "Os campos de um mesmo grupo saem na ordem do campo")]
    public void CamposDoGrupo_OrdenaPelaOrdemDoCampo()
    {
        FatoColetado[] campos = [Item("RENDA", 2), Item("PARENTESCO", 0), Item("NOME_DO_MEMBRO", 1)];

        OrdemCanonicaDaConfiguracao.CamposDoGrupo(campos).Select(static c => c.FatoCodigo).Should().Equal("PARENTESCO", "NOME_DO_MEMBRO", "RENDA");
    }

    [Fact(DisplayName = "O item sem seção, no formulário que as exige, é recusado na publicação")]
    public void PendenciaDosItensForaDeSecao_ItemSemSecao_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComFormularioDaInscricao();
        processo.DefinirFormulario(FormularioDeTeste.DeclaradoNoPapel(FinalidadeFormulario.Habilitacao, fase.Id), null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Habilitacao), [Item("DIPLOMA", 0, pagina: null)], null)
            .IsSuccess.Should().BeTrue();

        DomainError? recusa = PendenciasDosFormularios.PendenciaDosItensForaDeSecao(processo.Declaracao());

        recusa.Should().NotBeNull();
        recusa!.Code.Should().Be(FormularioProcessoErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "O formulário sem fase declarada é recusado na publicação")]
    public void PendenciaDaFaseDosFormularios_FormularioSemFase_Recusa()
    {
        (ProcessoSeletivo processo, _) = ProcessoComFormularioDaInscricao();
        processo.DefinirFormulario(FormularioDeTeste.DeclaradoNoPapel(FinalidadeFormulario.Habilitacao, null), null, FormularioDeTeste.Paginas(FinalidadeFormulario.Habilitacao))
            .IsSuccess.Should().BeTrue();

        DomainError? recusa = PendenciasDosFormularios.PendenciaDaFaseDosFormularios(processo.Declaracao());

        recusa.Should().NotBeNull();
        recusa!.Code.Should().Be(FormularioProcessoErrorCodes.SemFase);
    }
}
