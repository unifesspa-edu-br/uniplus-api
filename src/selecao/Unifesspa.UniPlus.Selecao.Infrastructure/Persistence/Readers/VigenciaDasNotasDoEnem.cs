namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// As consultas que decidem o que vale entre as notas do ENEM, compartilhadas pelas leituras de
/// lançamentos e de avaliações para que as duas concordem sobre a vigência: vale o lote mais recente
/// do escopo (<see cref="VigenciaDoLote"/>), e o que ele traz só vale enquanto a inscrição ainda
/// escolhe a edição do lote.
/// </summary>
internal static class VigenciaDasNotasDoEnem
{
    /// <summary>Os lotes que nenhum lote posterior do mesmo processo, etapa e edição substituiu.</summary>
    public static IQueryable<LoteDeNotasDoEnem> LotesVigentes(SelecaoDbContext context) =>
        context.LotesDeNotasDoEnem.Where(lote => !context.LotesDeNotasDoEnem.Any(mais =>
            mais.ProcessoSeletivoId == lote.ProcessoSeletivoId
            && mais.EtapaProcessoId == lote.EtapaProcessoId
            && mais.EdicaoCodigo == lote.EdicaoCodigo
            && (mais.ImportadoEm > lote.ImportadoEm
                || (mais.ImportadoEm == lote.ImportadoEm && mais.Id.CompareTo(lote.Id) > 0))));

    /// <summary>A edição do ENEM que cada inscrição escolheu, como a resposta do fato da edição a guarda.</summary>
    public static IQueryable<RespostaDaInscricaoRecord> EdicoesEscolhidas(SelecaoDbContext context) =>
        context.RespostasDaInscricao.Where(r => r.OcorrenciaId == null && r.FatoCodigo == EdicaoDoEnem.Fato);
}
