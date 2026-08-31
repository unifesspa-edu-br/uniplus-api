namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Handler da <see cref="ObterConformidadeLegalProcessoSeletivoQuery"/>: mesma dupla de
/// chamadas do gate de congelamento —
/// <see cref="IObrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync"/> +
/// <see cref="AvaliadorConformidadeLegal.Avaliar"/> — para que a leitura pública e a
/// transição nunca divirjam (Story #853, CA-16).
/// </summary>
/// <remarks>
/// A conferência do que a regra tem de ter para ser avaliada — forma do predicado e
/// existência das referências — entra aqui pelo mesmo motivo: a publicação recusa a
/// regra inavaliável, e sem repetir a conferência a consulta diria que o processo está
/// conforme instantes antes de o comando recusá-lo. A regra aparece reprovada, com o
/// motivo — reprovar é o que o avaliador faria se enxergasse o cadastro.
/// </remarks>
public static class ObterConformidadeLegalProcessoSeletivoQueryHandler
{
    public static async Task<ConformidadeLegalProcessoSeletivoDto?> Handle(
        ObterConformidadeLegalProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return null;
        }

        string tipoProcessoCodigo = processo.TipoProcesso.Codigo;

        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(tipoProcessoCodigo, query.DataReferencia, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, string> inavaliaveis = await ConferenciaDeReferenciasDasRegras
            .LevantarRegrasInavaliaveisAsync(
                regrasVigentes, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, cancellationToken)
            .ConfigureAwait(false);

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, tipoProcessoCodigo, regrasVigentes);

        RegraAvaliadaDto[] regrasDto = [.. resultado.Regras.Select(r => new RegraAvaliadaDto(
            r.RegraId,
            r.RegraCodigo,
            r.Categoria,
            r.TipoProcessoCodigoAvaliado,
            r.Predicado,
            r.Aprovada && !inavaliaveis.ContainsKey(r.RegraId),
            inavaliaveis.TryGetValue(r.RegraId, out string? inavaliavel)
                ? ConferenciaDeReferenciasDasRegras.MotivoDaInavaliabilidade(inavaliavel)
                : r.Motivo,
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
