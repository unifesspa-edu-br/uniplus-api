namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um formulário por finalidade (UNI-REQ-0144): cada fato tem um só produtor no processo, a ordem é
/// única dentro de cada formulário, a fase é a que a finalidade pede, e a publicação exige o
/// formulário de inscrição quando a inscrição é própria.
/// </summary>
public sealed class FormulariosPorFinalidadeTests
{
    private static FatoColetado Item(string codigo, int ordem, string? etapa = FormularioDeTeste.Secao) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, obrigatorio: false, null, etapaCodigo: etapa).Value!;

    private static ProcessoSeletivo ComHabilitacao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "O fato coletado por um formulário não é coletado por outro do mesmo processo")]
    public void ProdutorUnico_EntreFormularios()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Fact(DisplayName = "A ordem é única dentro de cada formulário: a mesma ordem em formulários diferentes é aceita")]
    public void Ordem_UnicaPorFormulario()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO_EMITIDO", 0)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("A", 1), Item("B", 1)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.OrdemDuplicada);
        processo.FatosColetados.Select(static f => (f.Finalidade, f.FatoCodigo)).Should().BeEquivalentTo(
            [(FinalidadeFormulario.Inscricao, "TEM_RENDA"), (FinalidadeFormulario.Habilitacao, "CERTIFICADO_EMITIDO")],
            "definir os itens de um formulário não mexe nos do outro");
    }

    private static TermoExigidoFormulario Termo(string codigo) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            null, Obrigatoriedade.Sempre).Value!;

    [Fact(DisplayName = "Itens e termos só entram em finalidade que tem formulário")]
    public void ItensETermos_SemFormulario_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFatosColetados(FinalidadeFormulario.IsencaoTaxa, [Item("A", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FormularioInexistente);
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.IsencaoTaxa, [Termo("DECLARACAO")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FormularioInexistente);
    }

    [Fact(DisplayName = "No rascunho, a recusa de seção aponta o item pelo índice dele na lista enviada")]
    public void Item_ForaDeSecao_ApontaOIndiceOriginal() =>
        ProcessoConformeFactory.Criar().DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, [Item("A", 0, etapa: null), Item("B", 1, "INEXISTENTE")], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Field.Should().Be("itens[1].etapaCodigo");

    [Fact(DisplayName = "Aviso da etapa acima do limite é recusado no próprio campo")]
    public void Etapa_AvisoLongo_RecusaNoCampoAviso() =>
        EtapaFormulario.Criar("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, new string('a', 2001))
            .Errors.Should().ContainSingle().Which.Field.Should().Be("aviso");

    private static FatoColetado ItemQueCita(string codigo, int ordem, string citado) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, obrigatorio: false,
            [CondicaoPrecondicaoFato.Criar(0, citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            etapaCodigo: FormularioDeTeste.Secao).Value!;

    [Fact(DisplayName = "Item de outra finalidade cita fato da inscrição; fato que nenhum dos dois coleta é recusado")]
    public void OutraFinalidade_CitaFatoDaInscricao()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_BOLSA")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "A inscrição não perde fato que um item de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Theory(DisplayName = "Item em bloco de sistema ou em etapa inexistente é recusado na definição")]
    [InlineData("REVISAO")]
    [InlineData("INEXISTENTE")]
    public void Item_ForaDeSecao_RecusaNaDefinicao(string etapa) =>
        ProcessoConformeFactory.Criar().DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("A", 0, etapa)], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);

    [Fact(DisplayName = "A fase do formulário está no cronograma e é a que a finalidade pede")]
    public void Fase_CoerenteComFinalidade()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid faseDeInscricao = processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, Guid.CreateVersion7(), null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseForaDoCronograma);
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, faseDeInscricao, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade);
    }

    [Fact(DisplayName = "Título, fase e estrutura do formulário são recusados juntos")]
    public void DefinirFormulario_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid faseDeInscricao = processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;
        EtapaFormulario soSecao = EtapaFormulario.Criar("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!;

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, faseDeInscricao, new string('a', 301), [soSecao], PrecondicaoIfMatch.Ausente)
            .Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [
                FormularioProcessoErrorCodes.TituloTamanho,
                FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade,
                EstruturaFormularioErrorCodes.BlocoExigidoAusente,
            ]);
    }

    [Fact(DisplayName = "Redefinir o formulário sem a seção de um item existente é recusado nas etapas")]
    public void DefinirFormulario_SemASecaoDeUmItem_RecusaNasEtapas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        EtapaFormulario outraSecao = EtapaFormulario.Criar("OUTRA", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Outra", null, null).Value!;
        EtapaFormulario revisao = EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null).Value!;

        processo.DefinirFormulario(
                FinalidadeFormulario.Inscricao, processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId, null, [outraSecao, revisao], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "etapas",
                Error = new { Code = EstruturaFormularioErrorCodes.ItemForaDeSecao },
            });
    }

    [Fact(DisplayName = "O cronograma recusa remover a fase em que um formulário é respondido")]
    public void Cronograma_RecusaRemoverFaseDoFormulario() =>
        ProcessoConformeFactory.Criar().DefinirCronogramaFases([ProcessoConformeFactory.FaseConforme()], [], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario);

    [Fact(DisplayName = "Processo com inscrição própria sem formulário de inscrição: item vermelho e publicação recusada")]
    public void Publicacao_ExigeFormularioDeInscricao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao()!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemFormulario);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(static i => i.Codigo == "formulario_inscricao_ausente" && !i.Ok);
    }

    [Fact(DisplayName = "Formulário sem fase é aceito no rascunho e recusado na publicação")]
    public void Publicacao_ExigeFaseDoFormulario()
    {
        ProcessoSeletivo processo = ComHabilitacao();

        processo.PendenciaPreCanonicalizacao()!.Code.Should().Be(FormularioProcessoErrorCodes.SemFase);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(static i => i.Codigo == "formulario_fase_incoerente" && !i.Ok);
    }

    [Fact(DisplayName = "Formulário de isenção só em processo que cobra taxa")]
    public void Publicacao_IsencaoExigeTaxa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(static i => i.Codigo == "formulario_isencao_sem_taxa" && !i.Ok);
    }

    [Fact(DisplayName = "Item sem seção é aceito no rascunho e recusado na publicação")]
    public void Publicacao_ExigeItemEmSecao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0, etapa: null)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao()!.Code.Should().Be(FormularioProcessoErrorCodes.ItemForaDeSecao);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario).Should().Contain(static i => i.Codigo == "formulario_item_fora_de_secao" && !i.Ok);
    }

    [Fact(DisplayName = "Remover o formulário em rascunho remove os itens e os termos dele")]
    public void Remover_LevaItensETermos()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO_EMITIDO", 0)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [Termo("DECLARACAO")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.RemoverFormulario(FinalidadeFormulario.Habilitacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull();
        processo.FatosColetados.Should().NotContain(static f => f.Finalidade == FinalidadeFormulario.Habilitacao);
        processo.TermosExigidos.Should().NotContain(static t => t.Finalidade == FinalidadeFormulario.Habilitacao);
    }
}
