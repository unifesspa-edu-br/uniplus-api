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
/// Os documentos que o edital exige do candidato, pela árvore de exigências congelada sobre as
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

    [Fact(DisplayName = "Num OU entre o RG e um E com a CNH e o comprovante, a CNH e o comprovante dividem o E dentro do OU")]
    public void ESobOu_OsDoisDocumentosDividemOE()
    {
        DocumentoExigido rg = Geral(), cnh = Geral(), comprovante = Geral();
        NoExigencia conjunto = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 1, null, null, [], [NoExigencia.CriarFolha(cnh, 0).Value!, NoExigencia.CriarFolha(comprovante, 1).Value!]).Value!;
        NoExigencia identidade = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, conjunto]).Value!;

        Dictionary<Guid, IReadOnlyList<GrupoDaArvore>> grupos = GruposPorDocumento(ComRaizes([identidade]));

        grupos[rg.Id].Should().Equal(new GrupoDaArvore(identidade.Id, TipoNo.GrupoOu, 1, null));
        grupos[cnh.Id].Should().Equal(new GrupoDaArvore(identidade.Id, TipoNo.GrupoOu, 1, null), new GrupoDaArvore(conjunto.Id, TipoNo.GrupoE, null, null));
        grupos[comprovante.Id].Should().Equal(grupos[cnh.Id], "os dois são uma alternativa só, e não duas");
    }

    [Fact(DisplayName = "Num N-de com um OU aninhado, cada documento traz os dois grupos, do mais externo ao mais interno")]
    public void OuAninhado_TrazOsDoisGrupos()
    {
        DocumentoExigido a = Geral(), b = Geral(), c = Geral();
        NoExigencia interno = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 1, 1, null, [], [NoExigencia.CriarFolha(b, 0).Value!, NoExigencia.CriarFolha(c, 1).Value!]).Value!;
        NoExigencia externo = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 2, null, [], [NoExigencia.CriarFolha(a, 0).Value!, interno]).Value!;

        Dictionary<Guid, IReadOnlyList<GrupoDaArvore>> grupos = GruposPorDocumento(ComRaizes([externo]));

        grupos[a.Id].Should().Equal(new GrupoDaArvore(externo.Id, TipoNo.GrupoOu, 2, null));
        grupos[b.Id].Should().Equal(new GrupoDaArvore(externo.Id, TipoNo.GrupoOu, 2, null), new GrupoDaArvore(interno.Id, TipoNo.GrupoOu, 1, null));
        grupos[c.Id].Should().Equal(grupos[b.Id]);
    }

    [Fact(DisplayName = "No OU repetido por membro, o OU traz a repetição; no OU entre exigências repetidas uma a uma, a repetição vem em cada folha")]
    public void Repeticao_DizOndeComeca()
    {
        DocumentoExigido rgX = Geral(), cnhX = Geral();
        NoExigencia porMembro = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, null, [], [NoExigencia.CriarFolha(rgX, 0).Value!, NoExigencia.CriarFolha(cnhX, 1).Value!],
            repetePorEntidade: Familia).Value!;
        DocumentoExigido rgY = Geral(), cnhY = Geral();
        NoExigencia deTodos = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 1, 1, null, [],
            [NoExigencia.CriarFolha(rgY, 0, repetePorEntidade: Familia).Value!, NoExigencia.CriarFolha(cnhY, 1, repetePorEntidade: Familia).Value!]).Value!;

        List<DocumentoAvaliado> documentos = [.. Avaliar(ComRaizes([porMembro, deTodos]), Respostas(), Membros(("m1", true))).Documentos.Itens];

        DocumentoAvaliado x = documentos.Single(d => d.Documento.Id == rgX.Id);
        x.Grupos.Should().Equal(new GrupoDaArvore(porMembro.Id, TipoNo.GrupoOu, 1, Familia));
        x.RepetePorEntidade.Should().BeNull("a repetição começa no OU: cada membro apresenta o RG ou a CNH");
        DocumentoAvaliado y = documentos.Single(d => d.Documento.Id == rgY.Id);
        y.Grupos.Should().Equal(new GrupoDaArvore(deTodos.Id, TipoNo.GrupoOu, 1, null));
        y.RepetePorEntidade.Should().Be(Familia, "a repetição começa na folha: o RG de todos ou a CNH de todos");
    }

    [Fact(DisplayName = "A exigência geral repetida por membro faz do grupo dependência da avaliação, com o que decide se ele aparece")]
    public void FolhaRepetidaSemGatilho_OGrupoEDependencia()
    {
        DocumentoExigido doMembro = Geral();
        PredicadoDnf soComBaixaRenda = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar(BaixaRenda, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

        DependenciasDaAvaliacao dependencias = AvaliacaoDaInscricao.Dependencias(
            ComRaizes([NoExigencia.CriarFolha(doMembro, 0, repetePorEntidade: Familia).Value!], exibicaoDaFamilia: soComBaixaRenda));

        dependencias.Grupos.Should().Equal(Familia);
        dependencias.Fatos.Should().Contain(BaixaRenda, "a resposta que mostra o grupo muda a quem se exige");
    }

    [Fact(DisplayName = "O derivado do sistema que decide se o grupo repetido aparece é apurado antes da avaliação")]
    public void ExibicaoDoGrupoRepetido_PedeODerivadoDoSistema()
    {
        PredicadoDnf soParaMenor = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar("FAIXA_ETARIA", Operador.Igual, JsonSerializer.SerializeToElement("MENOR_DE_IDADE")).Value!)]).Value!;

        IReadOnlySet<string> derivados = AvaliacaoDaInscricao.DerivadosDoSistemaCitados(
            ComRaizes([NoExigencia.CriarFolha(Geral(), 0, repetePorEntidade: Familia).Value!], exibicaoDaFamilia: soParaMenor));

        derivados.Should().Contain("FAIXA_ETARIA", "sem ela a consulta não decide se o grupo aparece, e a exigência por membro fica sem resposta");
    }

    private static Dictionary<Guid, IReadOnlyList<GrupoDaArvore>> GruposPorDocumento(ConfiguracaoAvaliavel configuracao) =>
        Avaliar(configuracao, Respostas()).Documentos.Itens.ToDictionary(static d => d.Documento.Id, static d => d.Grupos);

    private static DocumentoExigido Geral() =>
        DocumentoExigido.Criar(
            FaseDaInscricao.Id, Guid.CreateVersion7(), "DOCUMENTO", "Documento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, [], [], null, Qualquer, null).Value!;

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Enviados(params DocumentoExigido[] documentos) =>
        documentos.ToDictionary(static d => d.Id, static _ => (IReadOnlyList<ApresentacaoDocumento>)[new(Guid.CreateVersion7())]);

    [Fact(DisplayName = "A alternativa não enviada de um OU atendido, inclusive de um OU externo, é dispensada pelo grupo")]
    public void OuAtendido_DispensaAsAlternativasNaoEnviadas()
    {
        DocumentoExigido rg = Geral(), cnh = Geral(), passaporte = Geral();
        NoExigencia interno = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 1, 1, null, [], [NoExigencia.CriarFolha(cnh, 0).Value!, NoExigencia.CriarFolha(passaporte, 1).Value!]).Value!;
        NoExigencia externo = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, interno]).Value!;

        List<DocumentoAvaliado> comRg = [.. Avaliar(ComRaizes([externo]), Respostas(), apresentacoes: Enviados(rg)).Documentos.Itens];
        List<DocumentoAvaliado> semNada = [.. Avaliar(ComRaizes([externo]), Respostas()).Documentos.Itens];

        comRg.Single(d => d.Documento.Id == rg.Id).DispensadoPeloGrupo.Should().BeFalse("o enviado está atendido, não dispensado");
        comRg.Where(d => d.Documento.Id != rg.Id).Should().OnlyContain(static d => d.DispensadoPeloGrupo, "o RG atende o OU de fora, e a CNH e o passaporte não são necessários");
        semNada.Should().OnlyContain(static d => !d.DispensadoPeloGrupo);
    }

    [Fact(DisplayName = "O OU atendido por uma alternativa sem repetição dispensa a alternativa repetida por membro, em cada ocorrência")]
    public void OuAtendidoForaDaRepeticao_DispensaAAlternativaRepetida()
    {
        DocumentoExigido cadUnico = Geral(), renda = Geral();
        NoExigencia ou = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, null, [],
            [NoExigencia.CriarFolha(cadUnico, 0).Value!, NoExigencia.CriarFolha(renda, 1, repetePorEntidade: Familia).Value!]).Value!;

        List<DocumentoAvaliado> documentos = [.. Avaliar(
            ComRaizes([ou]), Respostas(), Membros(("m1", true), ("m2", true)), apresentacoes: Enviados(cadUnico)).Documentos.Itens];

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
        ConfiguracaoAvaliavel configuracao = ComRaizes([NoExigencia.CriarFolha(extrato, 0, quantidadeMinima: 2).Value!]);

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

    [Fact(DisplayName = "As dependências da avaliação cobrem o campo e o subitem que os gatilhos citam, e o grupo pelo qual a árvore se repete")]
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

    /// <param name="repetePorFamilia">Se a folha de <paramref name="documento"/> se repete por membro da família.</param>
    /// <param name="extra">Outra exigência, repetida por membro da família.</param>
    /// <param name="soNaHabilitacao">Outra exigência, fora de repetição.</param>
    private static ConfiguracaoAvaliavel Configuracao(
        DocumentoExigido documento, bool repetePorFamilia = false, DocumentoExigido? extra = null, DocumentoExigido? soNaHabilitacao = null)
    {
        List<NoExigencia> raizes = [NoExigencia.CriarFolha(documento, 0, repetePorEntidade: repetePorFamilia ? Familia : null).Value!];
        if (extra is not null)
        {
            raizes.Add(NoExigencia.CriarFolha(extra, 1, repetePorEntidade: Familia).Value!);
        }

        if (soNaHabilitacao is not null)
        {
            raizes.Add(NoExigencia.CriarFolha(soNaHabilitacao, 2).Value!);
        }

        return ComRaizes(raizes);
    }

    /// <param name="exibicaoDaFamilia">Quando o grupo da família aparece; sempre, quando nula.</param>
    private static ConfiguracaoAvaliavel ComRaizes(IReadOnlyList<NoExigencia> raizes, PredicadoDnf? exibicaoDaFamilia = null)
    {
        string etapa = DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, Secao);
        DefinicaoFormulario definicao = new(
            [new DefinicaoEtapa(
                etapa,
                null,
                [new DefinicaoItem(BaixaRenda, null, Obrigatoriedade.Sempre, []), new DefinicaoItem(PediuFator, null, Obrigatoriedade.Sempre, [])],
                [new DefinicaoGrupo(
                    Familia, exibicaoDaFamilia, Obrigatoriedade.Sempre, 0, null, [new DefinicaoItem(RendaDoMembro, null, Obrigatoriedade.Sempre, [])])])],
            [],
            []);

        return new ConfiguracaoAvaliavel(
            definicao,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            ArvoreExigenciasCongelada.DeGrafoReidratado(raizes),
            [Campo(BaixaRenda), Campo(PediuFator)],
            [GrupoColetado.Criar(Familia, 1, Secao, "Família", 0, null, null, Obrigatoriedade.Sempre, [Campo(RendaDoMembro, etapa: null)], FinalidadeFormulario.Inscricao).Value!],
            [],
            [FaseDaInscricao, FaseDaHabilitacao],
            [Formulario(FinalidadeFormulario.Inscricao, FaseDaInscricao), Formulario(FinalidadeFormulario.Habilitacao, FaseDaHabilitacao)]);
    }

    /// <param name="etapa">A seção do campo; o subitem de grupo segue a do grupo, e não tem a sua.</param>
    private static FatoColetado Campo(string fato, string? etapa = Secao) =>
        FatoColetado.Criar(
            fato, 0, $"Rótulo de {fato}", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, "PESSOAL",
            etapaCodigo: etapa, finalidade: FinalidadeFormulario.Inscricao).Value!;

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
            Guid.CreateVersion7(), ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: ordem == 1, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null, [], faseConcluinteCodigo: null, emiteParecerIndividual: false, [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);
}
