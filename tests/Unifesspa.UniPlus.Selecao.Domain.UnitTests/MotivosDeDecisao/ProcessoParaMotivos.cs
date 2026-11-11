namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Processo com o pedido de isenção aberto na fase de inscrição (a partir de 1º de março) e a
/// homologação das inscrições a partir de 1º de maio, para conferir a escolha dos motivos.
/// </summary>
internal static class ProcessoParaMotivos
{
    internal static readonly DateTimeOffset InicioDaInscricao = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset InicioDaHomologacao = new(2027, 5, 1, 0, 0, 0, TimeSpan.Zero);

    internal static ProcessoSeletivo Rascunho(params string[] fundamentos)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PSR Medicina 2027", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO", InicioDaInscricao, coletaIsencao: true), Fase(2, "HOMOLOGACAO", InicioDaHomologacao, coletaIsencao: false)],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        if (fundamentos.Length > 0)
        {
            processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(true, 100m, fundamentos).Value!, PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }

        return processo;
    }

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
            processo.Id, Encoding.UTF8.GetBytes("{}"), "1.1", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", InicioDaInscricao.AddDays(-30));
        processo.AbrirRetificacao("Inclusão de motivo", versaoBase, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SemEixosCongelados([publicada]), "user-sub-1", InicioDaInscricao.AddDays(-30))
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static MotivoDeDecisao Motivo(TipoMotivoDeDecisao tipo, string codigo, SentidoDoMotivo sentido, string? subclassificacao = null) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo, codigo, "Descrição do motivo " + codigo, subclassificacao, sentido)).Value!;

    internal static MotivoDeDecisao DaIsencao(string fundamento, SentidoDoMotivo sentido, string codigo = "RENDA_ACIMA_DO_LIMITE") =>
        Motivo(TipoMotivoDeDecisao.IsencaoDaTaxa, codigo, sentido, fundamento);

    private static FaseCronograma Fase(int ordem, string codigo, DateTimeOffset inicio, bool coletaIsencao) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: ordem == 1, coletaSolicitacaoIsencao: coletaIsencao,
            inicio: inicio, fim: inicio.AddDays(20),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
}
