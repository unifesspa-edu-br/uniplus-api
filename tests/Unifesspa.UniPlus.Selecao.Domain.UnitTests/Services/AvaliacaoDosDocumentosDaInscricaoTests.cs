namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os documentos que o edital exige do candidato, pelas regras de comprovação aplicadas às
/// respostas dele (UNI-REQ-0069, UNI-REQ-0077): o que se aplica, o que pode se aplicar e o que
/// depende de um fato que só se conhece depois da inscrição.
/// </summary>
public sealed class AvaliacaoDosDocumentosDaInscricaoTests
{
    private const string Secao = "DADOS";
    private const string BaixaRenda = "BAIXA_RENDA";
    private const string Familia = "FAMILIA";
    private const string RendaDoMembro = "RENDA_DO_MEMBRO";
    private const string PediuFator = "PEDIU_FATOR";

    /// <summary>A modalidade da convocação, que só existe depois do resultado: nenhum formulário a pergunta.</summary>
    private const string ModalidadeDaConvocacao = "MODALIDADE_CONVOCACAO";

    private static readonly FaseCronograma FaseDaInscricao = Fase(1, "INSCRICAO");
    private static readonly FaseCronograma FaseDaHabilitacao = Fase(3, "HABILITACAO");
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

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

    [Fact(DisplayName = "CNH e comprovante compartilham o grupo que exige ambos dentro do grupo alternativo")]
    public void ESobOu_OsDoisDocumentosDividemOE()
    {
        DocumentoExigido rg = Geral(), cnh = Geral(), comprovante = Geral();
        RegraDeComprovacao conjunto = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 1, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(cnh, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(comprovante, 1).Value!]).Value!;
        RegraDeComprovacao identidade = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, conjunto]).Value!;

        Dictionary<Guid, IReadOnlyList<GrupoDeComprovacao>> grupos = GruposPorDocumento(ComRegrasSemGrupo([identidade]));

        grupos[rg.Id].Should().Equal(new GrupoDeComprovacao(identidade.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null));
        grupos[cnh.Id].Should().Equal(new GrupoDeComprovacao(identidade.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), new GrupoDeComprovacao(conjunto.Id, FormaDeComprovacao.TodosOsItens, null, null));
        grupos[comprovante.Id].Should().Equal(grupos[cnh.Id], "os dois são uma alternativa só, e não duas");
    }

    [Fact(DisplayName = "Num N-de com um OU aninhado, cada documento traz os dois grupos, do mais externo ao mais interno")]
    public void GrupoAninhado_TrazOsDoisGrupos()
    {
        DocumentoExigido a = Geral(), b = Geral(), c = Geral();
        RegraDeComprovacao interno = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 1, 1, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(b, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(c, 1).Value!]).Value!;
        RegraDeComprovacao externo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 2, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(a, 0).Value!, interno]).Value!;

        Dictionary<Guid, IReadOnlyList<GrupoDeComprovacao>> grupos = GruposPorDocumento(ComRegrasSemGrupo([externo]));

        grupos[a.Id].Should().Equal(new GrupoDeComprovacao(externo.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 2, null));
        grupos[b.Id].Should().Equal(new GrupoDeComprovacao(externo.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 2, null), new GrupoDeComprovacao(interno.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null));
        grupos[c.Id].Should().Equal(grupos[b.Id]);
    }

    [Fact(DisplayName = "A repetição pode ser declarada no grupo ou em cada regra de documento")]
    public void Repeticao_DizOndeComeca()
    {
        DocumentoExigido rgX = Geral(), cnhX = Geral(), rgY = Geral(), cnhY = Geral();
        RegraDeComprovacao porMembro = GrupoRepetidoPorMembro(rgX, cnhX);
        RegraDeComprovacao deTodos = GrupoDeRegrasRepetidasPorMembro(rgY, cnhY);

        List<DocumentoAvaliado> documentos = [.. Avaliar(ComRegrasSemGrupo([porMembro, deTodos]), Respostas(), Membros(("m1", true))).Documentos.Itens];

        DocumentoAvaliado x = documentos.Single(d => d.Documento.Id == rgX.Id);
        x.Grupos.Should().Equal(new GrupoDeComprovacao(porMembro.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, Familia));
        x.GrupoDoFormularioParaRepeticao.Should().BeNull("a repetição começa no grupo: cada membro apresenta o RG ou a CNH");
        DocumentoAvaliado y = documentos.Single(d => d.Documento.Id == rgY.Id);
        y.Grupos.Should().Equal(new GrupoDeComprovacao(deTodos.Id, FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null));
        y.GrupoDoFormularioParaRepeticao.Should().Be(Familia, "a repetição começa na regra de documento: RG de todos ou CNH de todos");
    }

    [Fact(DisplayName = "A exigência geral repetida por membro faz do grupo dependência da avaliação, com o que decide se ele aparece")]
    public void RegraDeDocumentoRepetidaSemGatilho_OGrupoEDependencia()
    {
        DocumentoExigido doMembro = Geral();
        PredicadoDnf soComBaixaRenda = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar(BaixaRenda, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

        DependenciasDaAvaliacao dependencias = AvaliacaoDaInscricao.Dependencias(
            ComRegrasSemGrupo([RegraDeComprovacao.CriarRegraDeDocumento(doMembro, 0, grupoDoFormularioParaRepeticao: Familia).Value!], exibicaoDaFamilia: soComBaixaRenda));

        dependencias.Grupos.Should().Equal(Familia);
        dependencias.Fatos.Should().Contain(BaixaRenda, "a resposta que mostra o grupo muda a quem se exige");
    }

    [Fact(DisplayName = "O derivado do sistema que decide se o grupo repetido aparece é apurado antes da avaliação")]
    public void ExibicaoDoGrupoRepetido_PedeODerivadoDoSistema()
    {
        PredicadoDnf soParaMenor = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar("FAIXA_ETARIA", Operador.Igual, JsonSerializer.SerializeToElement("MENOR_DE_IDADE")).Value!)]).Value!;

        IReadOnlySet<string> derivados = AvaliacaoDaInscricao.DerivadosDoSistemaCitados(
            ComRegrasSemGrupo([RegraDeComprovacao.CriarRegraDeDocumento(Geral(), 0, grupoDoFormularioParaRepeticao: Familia).Value!], exibicaoDaFamilia: soParaMenor));

        derivados.Should().Contain("FAIXA_ETARIA", "sem ela a consulta não decide se o grupo aparece, e a exigência por membro fica sem resposta");
    }

    private static RegraDeComprovacao GrupoRepetidoPorMembro(DocumentoExigido primeiro, DocumentoExigido segundo) =>
        RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(primeiro, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(segundo, 1).Value!],
            grupoDoFormularioParaRepeticao: Familia).Value!;

    private static RegraDeComprovacao GrupoDeRegrasRepetidasPorMembro(DocumentoExigido primeiro, DocumentoExigido segundo) =>
        RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 1, 1, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(primeiro, 0, grupoDoFormularioParaRepeticao: Familia).Value!, RegraDeComprovacao.CriarRegraDeDocumento(segundo, 1, grupoDoFormularioParaRepeticao: Familia).Value!]).Value!;

    private static Dictionary<Guid, IReadOnlyList<GrupoDeComprovacao>> GruposPorDocumento(ConfiguracaoAvaliavel configuracao) =>
        Avaliar(configuracao, Respostas()).Documentos.Itens.ToDictionary(static d => d.Documento.Id, static d => d.Grupos);

    private static DocumentoExigido Geral() =>
        DocumentoExigido.Criar(
            FaseDaInscricao.Id, Guid.CreateVersion7(), "DOCUMENTO", "Documento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, [], [], null, Qualquer, null).Value!;

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Enviados(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(static d => d.Id, static _ => (IReadOnlyList<ApresentacaoDocumento>)[new(Guid.CreateVersion7())]);

    [Fact(DisplayName = "A alternativa não enviada de um OU atendido, inclusive de um OU externo, é dispensada pelo grupo")]
    public void GrupoCumprido_DispensaAsAlternativasNaoEnviadas()
    {
        DocumentoExigido rg = Geral(), cnh = Geral(), passaporte = Geral();
        RegraDeComprovacao interno = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 1, 1, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(cnh, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(passaporte, 1).Value!]).Value!;
        RegraDeComprovacao externo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, interno]).Value!;

        List<DocumentoAvaliado> comRg = [.. Avaliar(ComRegrasSemGrupo([externo]), Respostas(), apresentacoes: Enviados(rg)).Documentos.Itens];
        List<DocumentoAvaliado> semNada = [.. Avaliar(ComRegrasSemGrupo([externo]), Respostas()).Documentos.Itens];

        comRg.Single(d => d.Documento.Id == rg.Id).DispensadoPeloGrupo.Should().BeFalse("o enviado está atendido, não dispensado");
        comRg.Where(d => d.Documento.Id != rg.Id).Should().OnlyContain(static d => d.DispensadoPeloGrupo, "o RG atende o OU de fora, e a CNH e o passaporte não são necessários");
        semNada.Should().OnlyContain(static d => !d.DispensadoPeloGrupo);
    }

    [Fact(DisplayName = "O OU atendido por uma alternativa sem repetição dispensa a alternativa repetida por membro, em cada ocorrência")]
    public void GrupoCumpridoForaDaRepeticao_DispensaAAlternativaRepetida()
    {
        DocumentoExigido cadUnico = Geral(), renda = Geral();
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, null, [],
            [RegraDeComprovacao.CriarRegraDeDocumento(cadUnico, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(renda, 1, grupoDoFormularioParaRepeticao: Familia).Value!]).Value!;

        List<DocumentoAvaliado> documentos = [.. Avaliar(
            ComRegrasSemGrupo([grupo]), Respostas(), Membros(("m1", true), ("m2", true)), apresentacoes: Enviados(cadUnico)).Documentos.Itens];

        documentos.Where(d => d.Documento.Id == renda.Id).Should().HaveCount(2).And.OnlyContain(static d => d.DispensadoPeloGrupo);
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

    private static SituacaoDoDocumento Situacao(ResultadoDaAvaliacaoDaInscricao resultado, DocumentoExigido? documento) =>
        resultado.Documentos.Itens.Single(d => documento is null || d.Documento.Id == documento.Id).Situacao;

    private static ResultadoDaAvaliacaoDaInscricao Avaliar(
        ConfiguracaoAvaliavel configuracao,
        Dictionary<string, JsonElement> respostas,
        IReadOnlyList<OcorrenciaRespondida>? membros = null,
        bool naFaseDaInscricao = true,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos = new(StringComparer.Ordinal);
        if (membros is not null)
        {
            grupos[Familia] = membros;
        }

        Kernel.Results.Result<ResultadoDaAvaliacaoDaInscricao> resultado = AvaliacaoDaInscricao.Avaliar(
            configuracao,
            new EntradaDaAvaliacaoDaInscricao(
                respostas, grupos, new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal),
                Envio: null, Apresentacoes: apresentacoes ?? EntradaDaAvaliacaoDaInscricao.SemApresentacoes, NaFaseDaInscricao: naFaseDaInscricao));
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    private static Dictionary<string, JsonElement> Respostas(params (string Fato, bool Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);

    private static OcorrenciaRespondida[] Membros(params (string Id, bool Renda)[] membros) =>
        [.. membros.Select(static m => new OcorrenciaRespondida(m.Id, Respostas((RendaDoMembro, m.Renda))))];

    /// <param name="repetePorFamilia">Se a regra de <paramref name="documento"/> se repete por membro da família.</param>
    /// <param name="extra">Outra exigência, repetida por membro da família.</param>
    /// <param name="soNaHabilitacao">Outra exigência, fora de repetição.</param>
    private static ConfiguracaoAvaliavel Configuracao(
        DocumentoExigido documento, bool repetePorFamilia = false, DocumentoExigido? extra = null, DocumentoExigido? soNaHabilitacao = null)
    {
        List<RegraDeComprovacao> regrasSemGrupo = [RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, grupoDoFormularioParaRepeticao: repetePorFamilia ? Familia : null).Value!];
        if (extra is not null)
        {
            regrasSemGrupo.Add(RegraDeComprovacao.CriarRegraDeDocumento(extra, 1, grupoDoFormularioParaRepeticao: Familia).Value!);
        }

        if (soNaHabilitacao is not null)
        {
            regrasSemGrupo.Add(RegraDeComprovacao.CriarRegraDeDocumento(soNaHabilitacao, 2).Value!);
        }

        return ComRegrasSemGrupo(regrasSemGrupo);
    }

    /// <param name="exibicaoDaFamilia">Quando o grupo da família aparece; sempre, quando nula.</param>
    private static DefinicaoFormulario DefinicaoDoFormularioDaInscricao(PredicadoDnf? exibicaoDaFamilia) =>
        new(
            [new DefinicaoEtapa(
                DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, Secao),
                null,
                [new DefinicaoItem(BaixaRenda, null, Obrigatoriedade.Sempre, []), new DefinicaoItem(PediuFator, null, Obrigatoriedade.Sempre, [])],
                [DefinicaoDoGrupoDaFamilia(exibicaoDaFamilia)])],
            [],
            []);

    private static DefinicaoGrupo DefinicaoDoGrupoDaFamilia(PredicadoDnf? exibicao) => new(
        Familia, exibicao, Obrigatoriedade.Sempre, 0, null, [new DefinicaoItem(RendaDoMembro, null, Obrigatoriedade.Sempre, [])], incluiCandidato: false);

    /// <param name="exibicaoDaFamilia">Quando o grupo da família aparece; sempre, quando nula.</param>
    private static ConfiguracaoAvaliavel ComRegrasSemGrupo(IReadOnlyList<RegraDeComprovacao> regrasSemGrupo, PredicadoDnf? exibicaoDaFamilia = null) =>
        new(
            DefinicaoDoFormularioDaInscricao(exibicaoDaFamilia),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            RegrasDeComprovacaoDaVersao.DeGrafoReidratado(regrasSemGrupo),
            [Campo(BaixaRenda), Campo(PediuFator)],
            [GrupoDaFamilia()],
            [],
            [FaseDaInscricao, FaseDaHabilitacao],
            [Formulario(FinalidadeFormulario.Inscricao, FaseDaInscricao), Formulario(FinalidadeFormulario.Habilitacao, FaseDaHabilitacao)]);

    private static GrupoColetado GrupoDaFamilia() =>
        GrupoColetadoDeTeste.Agrupando(Familia, 1)
            .NaSecao(Secao)
            .ComRotulo("Família")
            .ComOcorrencias(0, null)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(Campo(RendaDoMembro, etapa: null))
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .Construir();

    /// <param name="etapa">A seção do campo; o subitem de grupo segue a do grupo, e não tem a sua.</param>
    private static FatoColetado Campo(string fato, string? etapa = Secao) =>
        FatoColetadoDeTeste.Coletando(fato, 0)
            .ComRotulo($"Rótulo de {fato}")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .NaSecao(etapa)
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .Construir();

    private static DocumentoExigido Condicional(FaseCronograma fase, params (string Fato, bool Valor)[] atomos) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE", "Comprovante", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            [.. atomos.Select(static a => CondicaoGatilho.Criar(0, a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!)],
            [], null, Qualquer, null).Value!;

    private static FormularioProcesso Formulario(FinalidadeFormulario finalidade, FaseCronograma fase) =>
        FormularioProcesso.Criar(
            finalidade, fase.Id, null,
            [EtapaFormulario.Criar("REVISAO", 0, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!]).Value!;

    private static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: ordem == 1, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []);
}
