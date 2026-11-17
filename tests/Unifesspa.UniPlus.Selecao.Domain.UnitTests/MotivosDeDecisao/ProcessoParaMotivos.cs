namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
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
        processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO", InicioDaInscricao, coletaIsencao: true), Fase(2, "HOMOLOGACAO_INSCRICOES", InicioDaHomologacao, coletaIsencao: false)],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(
            [Etapa("Prova objetiva", ordem: 1), Etapa("Prova discursiva", ordem: 2)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        if (fundamentos.Length > 0)
        {
            processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(true, 100m, fundamentos).Value!, PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    /// <summary>O processo recém-criado, sem fase nem etapa declaradas.</summary>
    internal static ProcessoSeletivo SemFasesNemEtapas() =>
        ProcessoSeletivo.Criar(
            "PSR Medicina 2027", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>O processo publicado, sem sessão de retificação aberta.</summary>
    internal static ProcessoSeletivo Publicado()
    {
        ProcessoSeletivo processo = Rascunho();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        return processo;
    }

    /// <summary>O processo publicado com a sessão de retificação aberta.</summary>
    internal static ProcessoSeletivo EmRetificacao()
    {
        ProcessoSeletivo processo = Rascunho();
        GrafoConfiguracao publicada = VersoesPublicadasDeTeste.DoProcesso(processo);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        VersaoConfiguracao versaoBase = VersaoConfiguracao.Abrir(
            processo.Id, new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), "1.1", "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "user-conta-1", InicioDaInscricao.AddDays(-30));
        processo.AbrirRetificacao("Inclusão de motivo", versaoBase, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SemEixosCongelados([publicada]), "user-conta-1", InicioDaInscricao.AddDays(-30))
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>O lugar da fase de código informado no processo.</summary>
    internal static LugarDaDecisao LugarDaFase(ProcessoSeletivo processo, string codigoDaFase) =>
        LugarDaDecisao.DaFase(processo.CronogramaFases.Single(f => f.Codigo == codigoDaFase).FaseCanonicaOrigemId);

    /// <summary>O lugar da etapa de nome informado no processo.</summary>
    internal static LugarDaDecisao LugarDaEtapa(ProcessoSeletivo processo, string nomeDaEtapa) =>
        LugarDaDecisao.DaEtapa(processo.Etapas.Single(e => e.Nome == nomeDaEtapa).Id);

    /// <summary>A escolha do motivo no lugar, entrando na versão do edital informada.</summary>
    internal static MotivoDoProcesso Escolha(ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem, int versaoDeEntrada) =>
        MotivoDoProcesso.Copiar(processo.Id, lugar, origem, versaoDeEntrada, justificativa: null);

    internal static MotivoDeDecisao Motivo(TipoMotivoDeDecisao tipo, string codigo, SentidoDoMotivo sentido, string? subclassificacao = null) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo.ToCodigo(), codigo, "Descrição do motivo " + codigo, subclassificacao, sentido)).Value!;

    internal static MotivoDeDecisao DaIsencao(string fundamento, SentidoDoMotivo sentido, string codigo = "RENDA_ACIMA_DO_LIMITE") =>
        Motivo(TipoMotivoDeDecisao.IsencaoDaTaxa, codigo, sentido, fundamento);

    private static EtapaProcesso Etapa(string nome, int ordem) =>
        ApoioDaPublicacaoDoProcessoSeletivo.EtapaDaProvaObjetiva(nome, CaraterEtapa.Classificatoria, peso: 1m, ordem: ordem, faseCodigo: "HOMOLOGACAO_INSCRICOES");

    private static FaseCronograma Fase(int ordem, string codigo, DateTimeOffset inicio, bool coletaIsencao) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: ordem == 1, ColetaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: inicio, Fim: inicio.AddDays(20), FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
}
