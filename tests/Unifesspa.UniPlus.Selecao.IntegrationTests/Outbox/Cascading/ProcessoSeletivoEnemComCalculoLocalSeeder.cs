namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoProcessoConformeSemeado;

/// <summary>
/// Variante de <see cref="ProcessoSeletivoPublicavelSeeder"/> cuja classificação é baseada em ENEM e
/// calcula a média pelos pesos por área, para provar a conformidade do grupo de área da oferta.
/// </summary>
internal static class ProcessoSeletivoEnemComCalculoLocalSeeder
{
    /// <summary>
    /// Igual à base, com classificação baseada em ENEM que calcula a média pelos pesos por área
    /// e uma oferta com ou sem grupo de área. Com grupo, o processo é conforme e publica; sem
    /// ele, fica vermelho só "Grupo de área do ENEM congelado em toda oferta", e
    /// <c>Publicar</c> recusa com <c>ProcessoSeletivo.ConformidadeInsuficiente</c>.
    /// </summary>
    public static async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearAsync(
        UniPlusDbContext db, string nome, bool ofertaComGrupo)
    {
        ProcessoSeletivo processo = ProcessoBaseConforme(
            db, nome,
            distribuicao: [DistribuicaoAmplaPadrao(db, ofertaComGrupo ? ("TECNOLOGICA", "Tecnológica") : null)]);

        DefinirClassificacaoEnem(processo, db);

        AdmitirEdicaoDoEnem(processo);

        DefinirCronogramaEEtapaDaFase(processo, [FaseDeResultadoFinal(coletaInscricao: true)]);

        Result itensResult = processo.DefinirItens([FormularioDeTeste.CampoDaEdicaoDoEnem(0)]);
        itensResult.IsSuccess.Should().BeTrue(itensResult.Error?.Message);

        return await PersistirComDocumentoConfirmadoAsync(db, processo);
    }

    private static void DefinirClassificacaoEnem(ProcessoSeletivo processo, UniPlusDbContext db)
    {
        Result<ConfiguracaoClassificacao> classificacaoResult = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = RegraDoCatalogo(db, RegraDeCalculoDaNota.MediaPonderada.Codigo()),
            RegraArredondamento = RegraDoCatalogo(db, RegraDeArredondamentoDaNota.Truncar.Codigo()),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = RegraDoCatalogo(db, RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo()),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        });
        classificacaoResult.IsSuccess.Should().BeTrue(classificacaoResult.Error?.Message);
        processo.DefinirClassificacao(classificacaoResult.Value!).IsSuccess.Should().BeTrue();
    }

    private static void AdmitirEdicaoDoEnem(ProcessoSeletivo processo)
    {
        // Classificação baseada em ENEM exige ao menos uma edição admitida.
        Result opcoesResult = processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!]);
        opcoesResult.IsSuccess.Should().BeTrue(opcoesResult.Error?.Message);
    }
}
