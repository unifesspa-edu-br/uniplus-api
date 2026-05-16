namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler convention-based de <see cref="ObterConformidadeAtualQuery"/>.
/// Carrega o edital com etapas/cotas (necessárias para o evaluator), busca
/// as regras vigentes aplicáveis ao tipo (universal + específico) na data
/// de hoje, roda o <see cref="ValidadorConformidadeEdital"/> e mapeia o
/// resultado para <see cref="ConformidadeDto"/>. Retorna <see langword="null"/>
/// quando o edital não existe — controller traduz para 404.
/// </summary>
public static class ObterConformidadeAtualQueryHandler
{
    public static async Task<ConformidadeDto?> Handle(
        ObterConformidadeAtualQuery query,
        IEditalRepository editalRepository,
        IObrigatoriedadeLegalRepository regraRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(editalRepository);
        ArgumentNullException.ThrowIfNull(regraRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Edital? edital = await editalRepository
            .ObterComEtapasECotasAsync(query.EditalId, cancellationToken)
            .ConfigureAwait(false);
        if (edital is null)
        {
            return null;
        }

        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.Date);
        string tipoEditalCodigo = edital.TipoEditalId?.ToString() ?? ObrigatoriedadeLegal.TipoEditalUniversal;

        IReadOnlyList<ObrigatoriedadeLegal> regras = await regraRepository
            .ObterVigentesParaTipoEditalAsync(tipoEditalCodigo, hoje, cancellationToken)
            .ConfigureAwait(false);

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(edital, regras);

        // Hash do payload precisa ser o canônico persistido em
        // ObrigatoriedadeLegal.Hash (HashCanonicalComputer per #460) — não o
        // placeholder textual emitido pelo ValidadorConformidadeEdital
        // (que vem do código #459 anterior à forma plena). Sem isso o
        // hash retornado por GET /conformidade divergiria do hash do
        // snapshot histórico e do catálogo admin, quebrando correlação
        // client-side (Codex P1).
        RegraAvaliadaDto[] regrasAvaliadas = [.. resultado.Regras.Select(avaliada =>
        {
            ObrigatoriedadeLegal? fonte = regras.FirstOrDefault(r => r.RegraCodigo == avaliada.RegraCodigo);
            return new RegraAvaliadaDto(
                RegraId: fonte?.Id ?? Guid.Empty,
                RegraCodigo: avaliada.RegraCodigo,
                Aprovada: avaliada.Aprovada,
                BaseLegal: avaliada.BaseLegal,
                PortariaInternaCodigo: avaliada.PortariaInterna,
                AtoNormativoUrl: fonte?.AtoNormativoUrl,
                DescricaoHumana: avaliada.DescricaoHumana,
                Hash: fonte?.Hash ?? avaliada.Hash,
                VigenciaInicio: fonte?.VigenciaInicio ?? hoje,
                VigenciaFim: fonte?.VigenciaFim);
        })];

        return new ConformidadeDto(edital.Id, regrasAvaliadas);
    }
}
