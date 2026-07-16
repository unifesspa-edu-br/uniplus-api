namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

/// <summary>
/// Handler da <see cref="ObterConformidadeLegalProcessoSeletivoQuery"/>: mesma dupla de
/// chamadas do gate de congelamento —
/// <see cref="IObrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync"/> +
/// <see cref="AvaliadorConformidadeLegal.Avaliar"/> — para que a leitura pública e a
/// transição nunca divirjam (Story #853, CA-16).
/// </summary>
public static class ObterConformidadeLegalProcessoSeletivoQueryHandler
{
    public static async Task<ConformidadeLegalProcessoSeletivoDto?> Handle(
        ObterConformidadeLegalProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return null;
        }

        string tipoProcessoCodigo = processo.Tipo.ToString();

        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(tipoProcessoCodigo, query.DataReferencia, cancellationToken)
            .ConfigureAwait(false);

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, tipoProcessoCodigo, regrasVigentes);

        RegraAvaliadaDto[] regrasDto = [.. resultado.Regras.Select(static r => new RegraAvaliadaDto(
            r.RegraId,
            r.RegraCodigo,
            r.Categoria,
            r.TipoProcessoCodigoAvaliado,
            r.Predicado,
            r.Aprovada,
            r.Motivo,
            r.BaseLegal,
            r.AtoNormativoUrl,
            r.PortariaInterna,
            r.DescricaoHumana,
            r.VigenciaInicio,
            r.VigenciaFim,
            r.Hash))];

        return new ConformidadeLegalProcessoSeletivoDto(
            processo.Id, query.DataReferencia, regrasDto, resultado.Avisos);
    }
}
