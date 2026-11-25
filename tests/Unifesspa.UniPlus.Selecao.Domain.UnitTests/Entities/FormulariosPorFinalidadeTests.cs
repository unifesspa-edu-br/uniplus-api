namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioFormulariosPorFinalidade;

/// <summary>
/// Um formulário por finalidade (UNI-REQ-0144): cada fato tem um só produtor no processo, a ordem é
/// única dentro de cada formulário, a fase é a que a finalidade pede, e a publicação exige o
/// formulário de inscrição quando a inscrição é própria.
/// </summary>
public sealed class FormulariosPorFinalidadeTests
{
    [Fact(DisplayName = "O fato coletado por um formulário não é coletado por outro do mesmo processo")]
    public void ProdutorUnico_EntreFormularios()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("TEM_RENDA", 0)])
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Fact(DisplayName = "A ordem é única dentro de cada formulário: a mesma ordem em formulários diferentes é aceita")]
    public void Ordem_UnicaPorFormulario()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO_EMITIDO", 0)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("A", 1), Item("B", 1)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.OrdemDuplicada);
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => (f.Finalidade, f.FatoCodigo)).Should().BeEquivalentTo(
            [(FinalidadeFormulario.Inscricao, "TEM_RENDA"), (FinalidadeFormulario.Habilitacao, "CERTIFICADO_EMITIDO")],
            "definir os itens de um formulário não mexe nos do outro");
    }

    [Fact(DisplayName = "Itens e termos só entram em finalidade que tem formulário")]
    public void ItensETermos_SemFormulario_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFatosColetados(FinalidadeFormulario.IsencaoTaxa, [Item("A", 0)])
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FormularioInexistente);
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.IsencaoTaxa, [Termo("DECLARACAO")])
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FormularioInexistente);
    }

    [Fact(DisplayName = "No rascunho, a recusa de seção aponta o item pelo índice dele na lista enviada")]
    public void Item_ForaDeSecao_ApontaOIndiceOriginal() =>
        ProcessoConformeFactory.Criar().DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, [Item("A", 0, pagina: null), Item("B", 1, "INEXISTENTE")])
            .Errors.Should().ContainSingle().Which.Field.Should().Be("itens[1].paginaCodigo");

    [Fact(DisplayName = "Aviso da pagina acima do limite é recusado no próprio campo")]
    public void Pagina_AvisoLongo_RecusaNoCampoAviso() =>
        PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, new string('a', 2001))
            .Errors.Should().ContainSingle().Which.Field.Should().Be("apresentacao.aviso");

    [Theory(DisplayName = "Item em bloco de sistema ou em pagina inexistente é recusado na definição")]
    [InlineData("REVISAO")]
    [InlineData("INEXISTENTE")]
    [InlineData("")]
    public void Item_ForaDeSecao_RecusaNaDefinicao(string pagina) =>
        ProcessoConformeFactory.Criar().DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("A", 0, pagina)])
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);

    [Fact(DisplayName = "A fase do formulário está no cronograma e é a que a finalidade pede")]
    public void Fase_CoerenteComFinalidade()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid faseDeInscricao = processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, Guid.CreateVersion7(), null, FormularioDeTeste.Paginas())
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseForaDoCronograma);
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, faseDeInscricao, null, FormularioDeTeste.Paginas())
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade);
    }

    [Fact(DisplayName = "Título, fase e estrutura do formulário são recusados juntos")]
    public void DefinirFormulario_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid faseDeInscricao = processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;
        PaginaDoFormulario soSecao = PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!;

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, faseDeInscricao, new string('a', 301), [soSecao])
            .Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [
                EstruturaFormularioErrorCodes.TituloTamanho,
                FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade,
                EstruturaFormularioErrorCodes.BlocoExigidoAusente,
            ]);
    }

    [Fact(DisplayName = "Redefinir o formulário sem a seção de um item existente é recusado nas paginas")]
    public void DefinirFormulario_SemASecaoDeUmItem_RecusaNasPaginas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        PaginaDoFormulario outraSecao = PaginaDoFormulario.Criar(new PaginaEstrutura("OUTRA", 1, TipoPaginaFormulario.Secao, null), "Outra", null, null).Value!;
        PaginaDoFormulario revisao = PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 2, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão", null, null).Value!;

        processo.DefinirFormulario(
                FinalidadeFormulario.Inscricao, processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId, null,
                [FormularioDeTeste.Paginas()[0], outraSecao, revisao])
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "paginas",
                Error = new { Code = EstruturaFormularioErrorCodes.ItemForaDeSecao },
            });
    }

    [Fact(DisplayName = "O cronograma recusa remover a fase em que um formulário é respondido")]
    public void Cronograma_RecusaRemoverFaseDoFormulario() =>
        ProcessoConformeFactory.Criar().DefinirCronogramaFases([ProcessoConformeFactory.FaseConforme()], [])
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario);

    [Fact(DisplayName = "Processo com inscrição própria sem formulário de inscrição: item vermelho e publicação recusada")]
    public void Publicacao_ExigeFormularioDeInscricao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemFormulario);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(static i => i.Codigo == "formulario_inscricao_ausente" && !i.Ok);
    }

    [Fact(DisplayName = "Formulário sem fase é aceito no rascunho e recusado na publicação")]
    public void Publicacao_ExigeFaseDoFormulario()
    {
        ProcessoSeletivo processo = ComHabilitacao();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(FormularioProcessoErrorCodes.SemFase);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(static i => i.Codigo == "formulario_fase_incoerente" && !i.Ok);
    }

    [Fact(DisplayName = "Formulário de isenção só em processo que cobra taxa")]
    public void Publicacao_IsencaoExigeTaxa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Paginas())
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(static i => i.Codigo == "formulario_isencao_sem_taxa" && !i.Ok);
    }

    [Fact(DisplayName = "Item sem seção é aceito no rascunho e recusado na publicação")]
    public void Publicacao_ExigeItemEmSecao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        // Direto no agregado: o helper de teste põe na seção de dados o item que vem sem ela.
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Inscricao,
                [Item("TEM_RENDA", FormularioDeTeste.PrimeiraOrdemDeInscricao, pagina: null), .. FormularioDeTeste.DadosBasicos()])
            .IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(FormularioProcessoErrorCodes.ItemForaDeSecao);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().Contain(static i => i.Codigo == "formulario_item_fora_de_secao" && !i.Ok);
    }

    [Fact(DisplayName = "Remover o formulário em rascunho remove os itens e os termos dele")]
    public void Remover_LevaItensETermos()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO_EMITIDO", 0)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [Termo("DECLARACAO")])
            .IsSuccess.Should().BeTrue();

        processo.RemoverFormulario(FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull();
        processo.FatosColetados.Should().NotContain(static f => f.Finalidade == FinalidadeFormulario.Habilitacao);
        processo.TermosExigidos.Should().NotContain(static t => t.Finalidade == FinalidadeFormulario.Habilitacao);
    }
}
