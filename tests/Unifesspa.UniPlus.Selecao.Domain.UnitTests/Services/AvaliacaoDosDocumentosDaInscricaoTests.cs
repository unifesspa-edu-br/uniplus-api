namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.ApoioDaAvaliacaoDosDocumentos;


/// <summary>
/// Os documentos que o edital exige do candidato, pelas regras de comprovação aplicadas às
/// respostas dele (UNI-REQ-0069, UNI-REQ-0077): o que se aplica, o que pode se aplicar e o que
/// depende de um fato que só se conhece depois da inscrição.
/// </summary>
public sealed class AvaliacaoDosDocumentosDaInscricaoTests
{
    [Fact(DisplayName = "A exigência cujo gatilho a resposta atende é exigida; a que ela não atende não é")]
    public void GatilhoAtendido_Exigido_GatilhoNaoAtendido_NaoExigido()
    {
        DocumentoExigido comprovante = Condicional(FaseDaInscricao, (BaixaRenda, true));

        Situacao(Avaliar(Configuracao(comprovante), Respostas((BaixaRenda, true))), comprovante).Should().Be(SituacaoDoDocumento.Pendente);
        Situacao(Avaliar(Configuracao(comprovante), Respostas((BaixaRenda, false))), comprovante).Should().Be(SituacaoDoDocumento.NaoExigido);
    }

    [Fact(DisplayName = "Sem a resposta que decide o gatilho, a exigência pode ser exigida, e a resposta é o que falta")]
    public void SemResposta_PodeSerExigido_EComPendencia()
    {
        DocumentoExigido comprovante = Condicional(FaseDaInscricao, (BaixaRenda, true));

        DocumentosDaInscricao documentos = Avaliar(Configuracao(comprovante), Respostas()).Documentos;

        documentos.Itens.Single(d => d.Documento.Id == comprovante.Id).Situacao.Should().Be(SituacaoDoDocumento.PodeSerExigido);
        documentos.PendenciasPorFase[FaseDaInscricao.Id].Select(static p => p.FatoCodigo).Should().Equal(BaixaRenda);
    }

    [Fact(DisplayName = "As pendências dos documentos são da fase de cada exigência: a resposta que só decide a habilitação não é pendência da inscrição")]
    public void Pendencias_SaoDaFaseDeCadaExigencia()
    {
        ConfiguracaoAvaliavel configuracao = Configuracao(
            Condicional(FaseDaInscricao, (BaixaRenda, true)),
            soNaHabilitacao: Condicional(FaseDaHabilitacao, (PediuFator, true)));

        DocumentosDaInscricao documentos = Avaliar(configuracao, Respostas()).Documentos;

        documentos.PendenciasPorFase[FaseDaInscricao.Id].Select(static p => p.FatoCodigo).Should().Equal(BaixaRenda);
        documentos.PendenciasPorFase[FaseDaHabilitacao.Id].Select(static p => p.FatoCodigo).Should().Equal(PediuFator);
    }

    [Fact(DisplayName = "Na inscrição, a exigência da habilitação que cita a modalidade da convocação depende de fase posterior")]
    public void FatoDeFasePosterior_DependeDeFasePosterior()
    {
        DocumentoExigido renda = Condicional(FaseDaHabilitacao, (ModalidadeDaConvocacao, true));

        DocumentosDaInscricao documentos = Avaliar(Configuracao(renda), Respostas()).Documentos;

        documentos.Itens.Single(d => d.Documento.Id == renda.Id).Situacao.Should().Be(SituacaoDoDocumento.DependeDeFasePosterior);
        documentos.PendenciasPorFase.Should().BeEmpty("nenhuma resposta do candidato decide o que só se conhece depois da inscrição");
    }

    [Fact(DisplayName = "A pré-visualização simula o perfil inteiro: a mesma exigência só pode ser exigida")]
    public void ForaDaFaseDaInscricao_NaoBloqueia() =>
        Situacao(
            Avaliar(Configuracao(Condicional(FaseDaHabilitacao, (ModalidadeDaConvocacao, true))), Respostas(), naFaseDaInscricao: false),
            null).Should().Be(SituacaoDoDocumento.PodeSerExigido);

    [Fact(DisplayName = "A exigência repetida por membro que cita a modalidade da convocação depende de fase posterior em cada membro")]
    public void RepetidaPorMembro_ComFatoDeFasePosterior_DependeEmCadaMembro()
    {
        DocumentoExigido extrato = Condicional(FaseDaHabilitacao, (RendaDoMembro, true), (ModalidadeDaConvocacao, true));

        List<DocumentoAvaliado> documentos = [.. Avaliar(
            Configuracao(extrato, repetePorFamilia: true),
            Respostas(),
            Membros(("m1", true), ("m2", true))).Documentos.Itens];

        documentos.Should().HaveCount(2);
        documentos.Should().OnlyContain(static d => d.Situacao == SituacaoDoDocumento.DependeDeFasePosterior && d.GrupoCodigo == Familia);
        documentos.Select(static d => d.OcorrenciaId).Should().Equal("m1", "m2");
    }

    [Fact(DisplayName = "Numa família com dois membros, a exigência por membro vale para quem atende ao gatilho")]
    public void FamiliaComDoisMembros_ExigeSoDeQuemAtende()
    {
        DocumentoExigido extrato = Condicional(FaseDaInscricao, (RendaDoMembro, true));

        List<DocumentoAvaliado> documentos = [.. Avaliar(
            Configuracao(extrato, repetePorFamilia: true),
            Respostas(),
            Membros(("m1", true), ("m2", false))).Documentos.Itens];

        documentos.Single(static d => d.OcorrenciaId == "m1").Situacao.Should().Be(SituacaoDoDocumento.Pendente);
        documentos.Single(static d => d.OcorrenciaId == "m2").Situacao.Should().Be(SituacaoDoDocumento.NaoExigido);
    }

    [Fact(DisplayName = "O documento enviado e confirmado deixa a exigência satisfeita; sem ele, segue pendente")]
    public void DocumentoEnviado_SatisfazAExigencia()
    {
        DocumentoExigido laudo = Condicional(FaseDaInscricao, (BaixaRenda, true));
        ConfiguracaoAvaliavel configuracao = Configuracao(laudo);
        Dictionary<string, JsonElement> respostas = Respostas((BaixaRenda, true));

        Situacao(Avaliar(configuracao, respostas), laudo).Should().Be(SituacaoDoDocumento.Pendente);
        Situacao(
            Avaliar(configuracao, respostas, apresentacoes: new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> { [laudo.Id] = [new(Guid.CreateVersion7())] }),
            laudo).Should().Be(SituacaoDoDocumento.Satisfeita);
    }

    [Fact(DisplayName = "A quantidade mínima conta apresentações: uma só segue pendente, e duas satisfazem")]
    public void QuantidadeMinima_ContaApresentacoes()
    {
        DocumentoExigido extrato = Geral();
        ConfiguracaoAvaliavel configuracao = ComRegrasSemGrupo([RegraDeComprovacao.CriarRegraDeDocumento(extrato, 0, quantidadeMinima: 2).Value!]);

        ResultadoDaAvaliacaoDaInscricao UmaApresentacao(int quantas) => Avaliar(
            configuracao,
            Respostas(),
            apresentacoes: new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>
            {
                [extrato.Id] = [.. Enumerable.Range(0, quantas).Select(static _ => new ApresentacaoDocumento(Guid.CreateVersion7()))],
            });

        Situacao(UmaApresentacao(1), extrato).Should().Be(SituacaoDoDocumento.Pendente);
        Situacao(UmaApresentacao(2), extrato).Should().Be(SituacaoDoDocumento.Satisfeita);
    }

    [Fact(DisplayName = "As dependências da avaliação cobrem o campo e o subitem que os gatilhos citam, e o grupo pelo qual as regras se repetem")]
    public void Dependencias_CobremOsGatilhos()
    {
        ConfiguracaoAvaliavel configuracao = Configuracao(
            Condicional(FaseDaInscricao, (BaixaRenda, true)),
            repetePorFamilia: false,
            extra: Condicional(FaseDaInscricao, (RendaDoMembro, true)));

        DependenciasDaAvaliacao dependencias = AvaliacaoDaInscricao.Dependencias(configuracao);

        dependencias.Fatos.Should().Equal(BaixaRenda, $"{Familia}.{RendaDoMembro}");
        dependencias.Grupos.Should().Equal(Familia);
    }
}
