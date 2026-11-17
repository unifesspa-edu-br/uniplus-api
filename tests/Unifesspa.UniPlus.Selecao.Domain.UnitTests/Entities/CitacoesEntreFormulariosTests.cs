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
/// O item e o termo de um formulário citam fatos coletados por outro: a citação precisa de um produtor no
/// processo, e a inscrição não perde o fato que outra finalidade cita.
/// </summary>
public sealed class CitacoesEntreFormulariosTests
{
    [Fact(DisplayName = "Item de outra finalidade cita fato da inscrição; fato que nenhum dos dois coleta é recusado")]
    public void OutraFinalidade_CitaFatoDaInscricao()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_RENDA")])
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_BOLSA")])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "A inscrição não perde fato que um item de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_RENDA")])
            .IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("COR_RACA", 0)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao)
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Item cita derivado cujas dependências são anteriores; derivado de campo posterior é recusado")]
    public void Item_CitaDerivado_PelasDependencias()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("TEM_RENDA", 0), ItemQueCitaODerivado(1)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([ItemQueCitaODerivado(0), Item("TEM_RENDA", 1)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact(DisplayName = "A publicação recusa item que cita derivado redefinido sobre campo posterior")]
    public void Publicacao_DerivacaoMudaDepoisDaCitacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("TEM_RENDA", 0), ItemQueCitaODerivado(1), Item("TEM_BOLSA", 2)])
            .IsSuccess.Should().BeTrue();
        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo).Should().BeNull();

        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_BOLSA")]).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact(DisplayName = "Termo cita os campos do próprio formulário e os da inscrição, nunca os de outra finalidade")]
    public void Termo_CitaSoOQueOFormularioConhece()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE_RENDA", 0)]).IsSuccess.Should().BeTrue();

        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA"), TermoQueCita("COMPROVANTE_RENDA", 1)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Inscricao, [TermoQueCita("COMPROVANTE_RENDA")])
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "termos[0]",
                Error = new { Code = GrafoFormularioErrorCodes.CitaFatoNaoConhecido },
            });
    }

    [Fact(DisplayName = "A inscrição não perde fato que um termo de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorTermoDeOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA")])
            .IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("COR_RACA", 0)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Citação de outro formulário que já era inválida não trava a edição da inscrição")]
    public void Inscricao_CitacaoJaInvalidaEmOutroFormulario_NaoTravaAEdicao()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Habilitacao, [ItemQueCitaODerivado(0), Item("COMPROVANTE_RENDA", 1)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("COMPROVANTE_RENDA")]).IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("TEM_RENDA", 0), Item("COR_RACA", 1)])
            .IsSuccess.Should().BeTrue("a troca da inscrição não tira nada que a habilitação conhecia");
    }

    [Fact(DisplayName = "Redefinir a inscrição mantendo o fato citado por outra finalidade é aceito")]
    public void Inscricao_MantemFatoCitadoPorOutraFinalidade_Aceita()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA")])
            .IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("TEM_RENDA", 0), Item("COR_RACA", 1)])
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Citação que a troca da inscrição invalida é recusada mesmo com outra citação já inválida no formulário")]
    public void Inscricao_InvalidaCitacaoComOutraJaInvalida_Recusa()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Habilitacao, [ItemQueCitaODerivado(0), Item("COMPROVANTE_RENDA", 1)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA")])
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("COMPROVANTE_RENDA")]).IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("COR_RACA", 0)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Remover da inscrição o fato citado por um item é recusado mesmo que outra citação do item já seja inválida")]
    public void Inscricao_ComparaCadaCitacaoDoItem()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCitaOsDois(), Item("COMPROVANTE_RENDA", 1)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("COMPROVANTE_RENDA")]).IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("COR_RACA", 0)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    private static FatoColetado ItemQueCitaOsDois() =>
        FatoColetado.Criar(
            "CONCORRER_EP", 0, "Concorrer", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [
                CondicaoPrecondicaoFato.Criar(0, "REGIME_DE_VAGA", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!,
                CondicaoPrecondicaoFato.Criar(0, "TEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ],
            etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}
