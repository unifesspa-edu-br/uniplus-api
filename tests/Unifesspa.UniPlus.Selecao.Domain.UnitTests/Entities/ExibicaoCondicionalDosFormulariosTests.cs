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
/// A exibição condicional da seção e do campo cita campos de seção anterior, e o campo opcional que
/// alimenta regra ou derivação deixa a configuração recusada na publicação.
/// </summary>
public sealed class ExibicaoCondicionalDosFormulariosTests
{
    [Fact(DisplayName = "Exibição de seção cita campo de seção anterior; campo da própria seção é recusado")]
    public void Secao_ExibicaoCitaSoOQueVemAntes()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao(null))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")]).IsSuccess.Should().BeTrue();

        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao("TEM_RENDA"))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao("RENDA_FORMAL"))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "paginas[2].exibicao",
                Error = new { Code = GrafoFormularioErrorCodes.CitaFatoPosterior },
            });
    }

    [Fact(DisplayName = "Estrutura recusada não esconde a recusa de exibição da seção: as duas saem juntas")]
    public void Secao_EstruturaEExibicaoInvalidas_AcumulaAsDuasRecusas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao(null))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")]).IsSuccess.Should().BeTrue();
        IReadOnlyList<PaginaDoFormulario> semOBlocoDeRevisao = [.. PaginasComExibicao("RENDA_FORMAL").Take(3)];

        Result resultado = processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, semOBlocoDeRevisao);

        resultado.Errors.Select(static e => e.Field).Should().Contain("paginas[2].exibicao")
            .And.Contain(static campo => campo != "paginas[2].exibicao", "a estrutura recusada também é informada");
    }

    [Fact(DisplayName = "Trocar os itens de modo que a seção passe a citar campo dela mesma é recusado")]
    public void Secao_ItensQueQuebramAExibicao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao(null))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao("TEM_RENDA"))
            .IsSuccess.Should().BeTrue();

        processo.DefinirItens([ItemNaSecao("RENDA_FORMAL", 0, "DADOS"), ItemNaSecao("TEM_RENDA", 1, "RENDA")])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact(DisplayName = "A inscrição não perde fato que a exibição de uma seção de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorSecaoDeOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, PaginasComExibicao("TEM_RENDA"))
            .IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("COR_RACA", 0)])
            .Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "A publicação recusa seção que cita derivado redefinido sobre campo da própria seção")]
    public void Publicacao_SecaoCitaDerivadoRedefinido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComExibicao(null))
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, PaginasComSecaoCondicionada()).IsSuccess.Should().BeTrue();
        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo).Should().BeNull();

        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("RENDA_FORMAL")]).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact(DisplayName = "Regra do formulário que cita a faixa etária exige a data de referência, como o gatilho")]
    public void Publicacao_RegraDoFormularioCitaFaixaEtariaSemReferencia_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirItens([DataDeNascimento(Obrigatoriedade.Sempre), CitaFaixaEtaria(1)])
            .IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");
    }

    [Fact(DisplayName = "Data de nascimento opcional que alimenta a faixa etária citada recusa a publicação; obrigatória, aceita")]
    public void Publicacao_DataDeNascimentoOpcionalQueAlimentaFaixaEtaria_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirReferenciaTemporalFatos(
                ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 12, 31), null).Value!)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens([DataDeNascimento(Obrigatoriedade.Nunca), CitaFaixaEtaria(1)])
            .IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);

        processo.DefinirItens([DataDeNascimento(Obrigatoriedade.Sempre), CitaFaixaEtaria(1)])
            .IsSuccess.Should().BeTrue();
        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Campo opcional que alimenta derivação recusa a publicação; obrigatório sempre que exibido, aceita")]
    public void Publicacao_CampoOpcionalQueAlimentaDerivacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirRegrasDerivacao([DerivadoQueDependeDe("TEM_RENDA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Opcional("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);

        processo.DefinirItens([Item("TEM_RENDA", 0)]).IsSuccess.Should().BeTrue();
        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Campo opcional citado por DIFERENTE em regra de outro campo recusa a publicação")]
    public void Publicacao_CampoOpcionalCitadoPorNegacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirItens([
            Opcional("NACIONALIDADE_BR", 0),
            Opcional("COMPROVANTE", 1, ExibicaoDeTeste.Quando("NACIONALIDADE_BR", Operador.Diferente, true)),
        ]).IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Message.Should().Contain("'NACIONALIDADE_BR'");
    }

    [Fact(DisplayName = "Bloco de sistema não tem exibição condicional")]
    public void Bloco_ComExibicao_Recusa() =>
        PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 2, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão", null, null,
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("TEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "exibicao",
                Error = new { Code = EstruturaFormularioErrorCodes.ExibicaoForaDeSecao },
            });

    private static IReadOnlyList<PaginaDoFormulario> PaginasComSecaoCondicionada() =>
        [
            FormularioDeTeste.Paginas()[0],
            PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 1, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
            PaginaDoFormulario.Criar(new PaginaEstrutura("RENDA", 2, TipoPaginaFormulario.Secao, null), "Renda", null, null,
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("REGIME_DE_VAGA", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!)]).Value!).Value!,
            PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 3, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
        ];
}
