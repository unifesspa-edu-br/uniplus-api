namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Processo com o pedido de isenção aberto na fase de inscrição (a partir de 1º de março), a homologação das inscrições a
/// partir de 1º de maio e duas etapas na fase de homologação, para conferir a escolha dos motivos nos lugares.
/// </summary>
internal static class ProcessoParaMotivos
{
    internal static readonly DateTimeOffset InicioDaInscricao = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset InicioDaHomologacao = new(2027, 5, 1, 0, 0, 0, TimeSpan.Zero);

    internal static ProcessoSeletivo Rascunho(params string[] fundamentos)
    {
        ProcessoSeletivo processo = SemFasesNemEtapas();
        processo.DefinirCronogramaFases(FasesQueDeclaramOsTiposDeDecisao(), []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(
            [Etapa("Prova objetiva", ordem: 1), Etapa("Prova discursiva", ordem: 2)]).IsSuccess.Should().BeTrue();
        if (fundamentos.Length > 0)
        {
            processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(true, 100m, fundamentos).Value!)
                .IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    private static FaseCronograma[] FasesQueDeclaramOsTiposDeDecisao() =>
    [
        Fase(1, "INSCRICAO", InicioDaInscricao, coletaIsencao: true, TipoMotivoDeDecisao.IsencaoDaTaxa),
        Fase(2, "HOMOLOGACAO_INSCRICOES", InicioDaHomologacao, coletaIsencao: false, TipoMotivoDeDecisao.HomologacaoDasInscricoes),
    ];

    /// <summary>A fase de homologação das inscrições passa a ter a regra de homologação e a não declarar o tipo de decisão dela.</summary>
    internal static void HomologandoSemDeclararOTipo(ProcessoSeletivo processo)
    {
        FaseCronograma homologacao = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), "HOMOLOGACAO_INSCRICOES", OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(2, Inicio: InicioDaHomologacao, Fim: InicioDaHomologacao.AddDays(20), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa() with { TiposDeDecisao = [] },
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

        processo.DefinirCronogramaFases([processo.CronogramaFases.Single(f => f.Codigo == "INSCRICAO"), homologacao], []).IsSuccess.Should().BeTrue();
    }

    /// <summary>O processo recém-criado, sem fase nem etapa declaradas.</summary>
    internal static ProcessoSeletivo SemFasesNemEtapas() =>
        ProcessoSeletivo.Criar(
            "PSR Medicina 2027", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>A fase declara só os tipos de decisão informados, no lugar dos que o processo de teste traz.</summary>
    internal static void DeclararNaFase(ProcessoSeletivo processo, string codigoDaFase, params TipoMotivoDeDecisao[] tipos)
    {
        FaseCronograma fase = processo.CronogramaFases.Single(f => f.Codigo == codigoDaFase);
        ProcessoCarregadoDoBanco.SemTiposDeDecisao(fase);
        Array.ForEach(tipos, tipo => ProcessoCarregadoDoBanco.ComTipoDeDecisao(fase, tipo));
    }

    /// <summary>A etapa declara só os tipos de decisão informados.</summary>
    internal static void DeclararNaEtapa(ProcessoSeletivo processo, string nomeDaEtapa, params TipoMotivoDeDecisao[] tipos) =>
        processo.Etapas.Single(e => e.Nome == nomeDaEtapa).DefinirTiposDeDecisao([.. tipos.Select(static tipo => tipo.Codigo())]).IsSuccess.Should().BeTrue();

    /// <summary>O processo sem a fase de código informado, como fica o retrato dos lugares quando a leitura recusa a fase inteira.</summary>
    internal static void SemAFase(ProcessoSeletivo processo, string codigoDaFase) =>
        processo.DefinirCronogramaFases([.. processo.CronogramaFases.Where(f => f.Codigo != codigoDaFase)], []).IsSuccess.Should().BeTrue();

    /// <summary>O processo sem a etapa de nome informado, como fica o retrato dos lugares quando a leitura recusa a etapa inteira.</summary>
    internal static void SemAEtapa(ProcessoSeletivo processo, string nomeDaEtapa) =>
        processo.DefinirEtapas([.. processo.Etapas.Where(e => e.Nome != nomeDaEtapa)]).IsSuccess.Should().BeTrue();

    /// <summary>Os lugares da decisão do processo como ele está.</summary>
    internal static LugaresDaDecisao Lugares(ProcessoSeletivo processo) => LugaresDaDecisao.Das(processo.CronogramaFases, processo.Etapas);

    /// <summary>O processo publicado.</summary>
    internal static ProcessoSeletivo Publicado()
    {
        ProcessoSeletivo processo = Rascunho();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Situacao))!.SetValue(processo, SituacaoDoProcessoSeletivo.Publicado);
        return processo;
    }

    /// <summary>O lugar da fase de código informado no processo.</summary>
    internal static LugarDaDecisao LugarDaFase(ProcessoSeletivo processo, string codigoDaFase) =>
        LugarDaDecisao.DaFase(processo.CronogramaFases.Single(f => f.Codigo == codigoDaFase).FaseCanonicaOrigemId);

    /// <summary>O lugar da etapa de nome informado no processo.</summary>
    internal static LugarDaDecisao LugarDaEtapa(ProcessoSeletivo processo, string nomeDaEtapa) =>
        LugarDaDecisao.DaEtapa(processo.Etapas.Single(e => e.Nome == nomeDaEtapa).Id);

    /// <summary>A escolha do motivo no lugar, entrando na versão do edital informada.</summary>
    internal static MotivoDeDecisaoDoProcesso Escolha(ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem, int versaoDeEntrada) =>
        MotivoDeDecisaoDoProcesso.Copiar(processo.Id, lugar, origem, versaoDeEntrada, justificativa: null);

    internal static MotivoDeDecisao Motivo(TipoMotivoDeDecisao tipo, string codigo, SentidoDoMotivo sentido, string? subclassificacao = null) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo.Codigo(), codigo, "Descrição do motivo " + codigo, subclassificacao, sentido)).Value!;

    internal static MotivoDeDecisao DaIsencao(string fundamento, SentidoDoMotivo sentido, string codigo = "RENDA_ACIMA_DO_LIMITE") =>
        Motivo(TipoMotivoDeDecisao.IsencaoDaTaxa, codigo, sentido, fundamento);

    private static EtapaProcesso Etapa(string nome, int ordem) =>
        ApoioDaPublicacaoDoProcessoSeletivo.EtapaDaProvaObjetiva(nome, CaraterEtapa.Classificatoria, peso: 1m, ordem: ordem, faseCodigo: "HOMOLOGACAO_INSCRICOES");

    private static FaseCronograma Fase(int ordem, string codigo, DateTimeOffset inicio, bool coletaIsencao, TipoMotivoDeDecisao tipoDeDecisao) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: ordem == 1, coletaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: inicio, Fim: inicio.AddDays(20), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(
                Produtos: [ProdutoDaFase.Criar(codigo, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: [tipoDeDecisao.Codigo()]),
            bancasRequeridas: []).Value!;
}
