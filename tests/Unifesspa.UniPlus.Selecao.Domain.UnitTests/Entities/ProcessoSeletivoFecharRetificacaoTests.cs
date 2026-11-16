namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaRetificacaoDoProcessoSeletivo;

/// <summary>
/// Cobertura do fechamento da sessão de retificação de <see cref="ProcessoSeletivo"/>: a
/// configuração viva muda durante a sessão e é o fechamento que a reavalia e congela.
/// </summary>
public sealed class ProcessoSeletivoFecharRetificacaoTests
{
    [Fact(DisplayName = "Story #554/PR #903: fechar retificação com exigência configurada durante a sessão é aceito — a guarda B-01 foi removida")]
    public void FecharRetificacao_ExigenciaConfiguradaNaSessao_Aceita()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out VersaoConfiguracao versaoAbertura);
        AbrirRetificacaoComSucesso(processo, versaoAbertura, "Incluir exigência documental", clock);

        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaDeIdentidadeObrigatoria(processo.CronogramaFases.Single().Id), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");
        Result<VersaoConfiguracao> resultado = FecharUmMinutoDepois(processo, versaoAbertura, clock);

        resultado.IsSuccess.Should().BeTrue(
            resultado.Error?.Message ?? "o bloco documentosExigidos.exigencias deixou de ser stub — nada mais bloqueia este fechamento");
        processo.Rascunho.Should().BeNull("o fechamento aceito encerra a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação com uma fase sem unidade responsável recusa e a sessão segue aberta")]
    public void FecharRetificacao_FaseSemUnidadeResponsavel_RecusaESessaoSegueAberta()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoHaUmMinuto(clock, out VersaoConfiguracao versaoAbertura);
        AbrirRetificacaoComSucesso(processo, versaoAbertura, "Trocar a unidade responsável pela matrícula", clock);

        TirarAUnidadeResponsavelDaFaseNaSessao(processo);
        Result<VersaoConfiguracao> resultado = FecharUmMinutoDepois(processo, versaoAbertura, clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseSemUnidadeResponsavel");
        processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "CA-05 (5/5): mudar o gatilho de AC para LB_PPI numa retificação reavalia a coerência consequência↔ação — não fica cacheada da publicação anterior")]
    public void FecharRetificacao_MudancaDeGatilhoTornaConsequenciaIncoerente_Reavalia()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicadoComGatilhoEmAc(clock, out VersaoConfiguracao versaoAbertura);
        AbrirRetificacaoComSucesso(
            processo, versaoAbertura, "Estender exigência de heteroidentificação para a cota PPI", clock);

        ExigirHeteroidentificacaoDaModalidade(processo, "LB_PPI", PrecondicaoIfMatch.Curinga);
        Result<VersaoConfiguracao> resultado = FecharUmMinutoDepois(processo, versaoAbertura, clock);

        resultado.IsFailure.Should().BeTrue(
            "a coerência é recomputada a cada fechamento, a partir da coleção real de exigências — não fica " +
            "cacheada do estado coerente aprovado na publicação anterior");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
        processo.Rascunho.Should().NotBeNull("o fechamento recusado não encerra a sessão editorial");
    }

    private static Result<VersaoConfiguracao> FecharUmMinutoDepois(
        ProcessoSeletivo processo, VersaoConfiguracao versaoAbertura, RelogioManual clock)
    {
        clock.Avancar(TimeSpan.FromMinutes(1));
        return FecharRetificacaoComDadosPadrao(processo, versaoAbertura, clock);
    }

    // Trocar a unidade depois de publicar é retificação, e a sessão aceita o rascunho sem ela:
    // quem recusa é o fechamento, que congela a versão nova.
    private static void TirarAUnidadeResponsavelDaFaseNaSessao(ProcessoSeletivo processo)
    {
        FaseCronograma semUnidade = FaseSemUnidadeResponsavelComOMesmoConteudoDe(processo.CronogramaFases.Single());
        Result cronograma = processo.DefinirCronogramaFases([semUnidade], [], PrecondicaoIfMatch.Curinga);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
    }

    // Mesma consequência ELIMINA nas duas exigências: só o gatilho muda, de AC (ação null, coerente)
    // para LB_PPI (ação RECLASSIFICA_AC, incoerente com ELIMINA).
    private static ProcessoSeletivo NovoProcessoPublicadoComGatilhoEmAc(RelogioManual clock, out VersaoConfiguracao versaoAbertura)
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RECLASSIFICA_AC");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        ExigirHeteroidentificacaoDaModalidade(processo, "AC", PrecondicaoIfMatch.Ausente);

        Result<VersaoConfiguracao> publicacao = PublicarComDadosPadrao(processo, clock);
        publicacao.IsSuccess.Should().BeTrue(
            publicacao.Error?.Message ?? "AC não declara AcaoQuandoIndeferido — nada com que a exigência seja incoerente");
        versaoAbertura = publicacao.Value!;
        processo.DequeueDomainEvents();
        clock.Avancar(TimeSpan.FromMinutes(1));
        return processo;
    }

    private static void ExigirHeteroidentificacaoDaModalidade(
        ProcessoSeletivo processo, string modalidadeCodigo, PrecondicaoIfMatch precondicao)
    {
        DocumentoExigido exigencia = ExigenciaCondicionalPorModalidadeComConsequencia(
            processo.CronogramaFases.Single().Id, "AUTODECLARACAO_ETNICO_RACIAL", "Autodeclaração étnico-racial",
            "HETEROIDENTIFICACAO", modalidadeCodigo, "ELIMINA");

        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], precondicao).IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido ExigenciaDeIdentidadeObrigatoria(Guid exigidoNaFaseId) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static FaseCronograma FaseSemUnidadeResponsavelComOMesmoConteudoDe(FaseCronograma atual) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            atual.FaseCanonicaOrigemId, atual.Codigo, atual.OrigemData, atual.AgrupaEtapas, atual.PermiteComplementacao, atual.ColetaInscricao, atual.ColetaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(atual.Ordem, atual.Inicio, atual.Fim, atual.FaseConcluinteCodigo, UnidadeResponsavel: null),
        new PublicacoesDaFase([.. atual.Produtos], atual.EmiteParecerIndividual, atual.RegraRecurso),
        [.. atual.BancasRequeridas]).Value!;
}
