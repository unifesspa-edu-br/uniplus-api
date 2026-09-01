namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
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
        ITipoDeficienciaReader tipoDeficienciaReader,
        IRegraCatalogoReader regraCatalogoReader,
        IResolvedorFusoInstitucional resolvedorFuso,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return null;
        }

        string tipoProcessoCodigo = processo.TipoProcesso.Codigo;

        // Sem data explícita, a consulta responde pelo mesmo dia que o gate de publicação usaria
        // (issue #1350). Derivar aqui, e não deixar o chamador informar, é o que impede o
        // preflight de dizer "conforme" numa data que o comando contradiz.
        DateOnly? dataReferencia = query.DataReferencia
            ?? DiaDeReferenciaLegal(processo, query.PeriodoInscricaoInformado, resolvedorFuso);
        if (dataReferencia is not { } diaDeReferencia)
        {
            return null;
        }

        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(tipoProcessoCodigo, diaDeReferencia, cancellationToken)
            .ConfigureAwait(false);

        ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias referencias = await ConferenciaDeReferenciasDasRegras
            .LevantarAsync(
                regrasVigentes,
                modalidadeReader,
                tipoDocumentoReader,
                tipoEtapaReader,
                tipoDeficienciaReader,
                regraCatalogoReader,
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, string> inavaliaveis = referencias.RegrasInavaliaveis;

        // Mesmo relatório do gate: a regra que aparece reprovada aqui é a mesma que
        // bloqueia a publicação, inclusive quando o motivo é identidade e não código.
        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, tipoProcessoCodigo, regrasVigentes, referencias.IdentidadeDoTipoDocumentoPorCodigo);

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
            processo.Id, diaDeReferencia, regrasDto, resultado.Avisos);
    }

    /// <summary>
    /// O dia que o gate de publicação usaria, pela MESMA resolução dele: a janela da fase que
    /// coleta inscrição quando ela existe, o período informado no ato quando não existe.
    /// </summary>
    /// <remarks>
    /// Espelhar o gate é o que sustenta a garantia do CA-16/CA-17. Olhar só o cronograma deixaria
    /// o certame de importação externa — que não tem fase de coleta e informa o período — sem data,
    /// e o 422 dele sairia sem a lista de regras reprovadas.
    /// </remarks>
    private static DateOnly? DiaDeReferenciaLegal(
        ProcessoSeletivo processo,
        DateTimeOffset? periodoInscricaoInformado,
        IResolvedorFusoInstitucional resolvedorFuso)
    {
        if ((processo.FaseQueAncoraOPeriodoDeInscricao()?.Inicio ?? periodoInscricaoInformado) is not { } inicio)
        {
            return null;
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            // Zona irresolvível é defeito de instalação, e os gates de publicação a mapeiam para
            // 500. Devolver null aqui viraria 404 — a consulta diria que o processo não existe e
            // esconderia a falha de configuração que ela deveria justamente antecipar.
            throw new InvalidOperationException(fuso.Error!.Message);
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(inicio, fuso.Value!).DateTime);
    }

}
