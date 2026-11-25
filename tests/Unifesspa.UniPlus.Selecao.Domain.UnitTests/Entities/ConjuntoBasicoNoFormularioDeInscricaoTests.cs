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

/// <summary>
/// O formulário de inscrição coleta o conjunto básico do candidato: nasce com ele, e a publicação
/// recusa o formulário de inscrição a que falta algum dado básico.
/// </summary>
public sealed class ConjuntoBasicoNoFormularioDeInscricaoTests
{
    [Theory(DisplayName = "O formulário de inscrição sem algum dado básico é recusado na publicação e no checklist")]
    [InlineData(null)]
    [InlineData("NOME_PAI")]
    [InlineData("SEXO_CIVIL")]
    [InlineData("IDENTIDADE_GENERO")]
    [InlineData("ORIENTACAO_SEXUAL")]
    public void Publicacao_InscricaoSemDadoBasico_Recusa(string? semODado)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, FormularioDeTeste.DadosBasicos(semODado is null ? [] : [semODado]))
            .IsSuccess.Should().BeTrue();

        DomainError? pendencia = CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo);

        (pendencia?.Code).Should().Be(semODado is null ? null : FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(static i => i.Codigo == "formulario_inscricao_sem_conjunto_basico").Ok.Should().Be(semODado is null);
    }

    [Fact(DisplayName = "O dado básico gravado fora da seção reservada é recusado na publicação")]
    public void Publicacao_DadoBasicoForaDaSecao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        FatoColetado nome = FormularioDeTeste.DadosBasicos()[0];
        FatoColetado nomeEmDados = FatoColetadoDeTeste.Coletando(nome.FatoCodigo, FormularioDeTeste.PrimeiraOrdemDeInscricao)
            .ComRotulo(nome.Rotulo)
            .DoTipo(nome.TipoDeCampo)
            .ComFormato(nome.Formato)
            .ComObrigatoriedade(nome.Obrigatoriedade)
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, [.. FormularioDeTeste.DadosBasicos([nome.FatoCodigo]), nomeEmDados])
            .IsSuccess.Should().BeTrue();

        CondicoesDaCanonicalizacaoDeTeste.PrimeiraCausa(processo)!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);
    }

    [Theory(DisplayName = "O formulário de inscrição nasce com os dados básicos, e uma seção já pode citá-los")]
    [InlineData(true)]
    [InlineData(false)]
    public void DefinirFormulario_CriacaoComOsDadosBasicos_SecaoCitaDadoBasico(bool comOsDadosBasicos)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao).IsSuccess.Should().BeTrue();
        IReadOnlyList<EtapaFormulario> etapas = EtapasComSecaoDeRevalidacao();

        Result resultado = processo.DefinirFormulario(
            FinalidadeFormulario.Inscricao, fase, null, etapas, comOsDadosBasicos ? FormularioDeTeste.DadosBasicos() : null);

        ConferirDadosBasicos(processo, resultado, comOsDadosBasicos);
    }

    [Fact(DisplayName = "Criar a inscrição quando outro formulário coleta um dado básico é recusado, e a recusa orienta")]
    public void DefinirFormulario_DadoBasicoEmOutroFormulario_RecusaOrientando()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("EMAIL", 0).ComRotulo("E-mail").DeTexto("EMAIL").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir()],
            finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirFormulario(
            FinalidadeFormulario.Inscricao, fase, null, FormularioDeTeste.Etapas(), FormularioDeTeste.DadosBasicos());

        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
        resultado.Error.Message.Should().Contain("dados básicos").And.Contain("HABILITACAO");
    }

    private static IReadOnlyList<EtapaFormulario> EtapasComSecaoDeRevalidacao()
    {
        PredicadoDnf estrangeiro = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("NACIONALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("ESTRANGEIRO")).Value!)]).Value!;
        IReadOnlyList<EtapaFormulario> etapas =
        [
            FormularioDeTeste.Etapas()[0],
            EtapaFormulario.Criar(new EtapaEstrutura("REVALIDACAO", 1, TipoEtapaFormulario.Secao, null), "Revalidação", null, null, estrangeiro).Value!,
            EtapaFormulario.Criar(new EtapaEstrutura("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
        ];
        return etapas;
    }


    private static void ConferirDadosBasicos(ProcessoSeletivo processo, Result resultado, bool comOsDadosBasicos)
    {
        if (comOsDadosBasicos)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
            processo.FatosColetados.Select(static f => f.FatoCodigo).Should().BeEquivalentTo(ConjuntoBasicoDaInscricao.Fatos);
        }
        else
        {
            resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
            processo.FormularioDe(FinalidadeFormulario.Inscricao).Should().BeNull("a recusa não cria o formulário pela metade");
        }
    }

}
