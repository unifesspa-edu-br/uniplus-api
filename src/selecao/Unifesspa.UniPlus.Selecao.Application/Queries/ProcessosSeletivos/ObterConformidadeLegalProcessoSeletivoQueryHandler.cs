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
/// Distingue 404 (processo inexistente) de 422 (não há data de referência de onde partir) —
/// mesmo contrato de erro de <see cref="ObterFormularioRenderizavelQueryHandler"/>. Traduzir a
/// segunda condição em 404 diria que o processo não existe e esconderia a pendência que a
/// consulta deveria justamente antecipar (issue #1456).
/// <para>
/// A conferência do que a regra tem de ter para ser avaliada — forma do predicado e
/// existência das referências — entra aqui pelo mesmo motivo: a publicação recusa a
/// regra inavaliável, e sem repetir a conferência a consulta diria que o processo está
/// conforme instantes antes de o comando recusá-lo. A regra aparece reprovada, com o
/// motivo — reprovar é o que o avaliador faria se enxergasse o cadastro.
/// </para>
/// </remarks>
public static class ObterConformidadeLegalProcessoSeletivoQueryHandler
{
    public static async Task<Result<ConformidadeLegalProcessoSeletivoDto>> Handle(
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
            return Result<ConformidadeLegalProcessoSeletivoDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        string tipoProcessoCodigo = processo.TipoProcesso.Codigo;

        // Sem data explícita, a consulta responde pelo mesmo dia que o gate de publicação usaria
        // (issue #1350). Derivar aqui, e não deixar o chamador informar, é o que impede o
        // preflight de dizer "conforme" numa data que o comando contradiz.
        Result<DateOnly> referencia = query.DataReferencia is { } informada
            ? Result<DateOnly>.Success(informada)
            : DiaDeReferenciaLegal(processo, query.PeriodoInscricaoInformado, resolvedorFuso);
        if (referencia.IsFailure)
        {
            return Result<ConformidadeLegalProcessoSeletivoDto>.Failure(referencia.Error!);
        }

        DateOnly diaDeReferencia = referencia.Value!;

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
            processo, tipoProcessoCodigo, regrasVigentes, referencias.Identidades);

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

        return Result<ConformidadeLegalProcessoSeletivoDto>.Success(new ConformidadeLegalProcessoSeletivoDto(
            processo.Id, diaDeReferencia, regrasDto, resultado.Avisos));
    }

    /// <summary>
    /// O dia que o gate de publicação usaria, pela MESMA resolução dele: a janela da fase que
    /// coleta inscrição quando ela existe, o período informado no ato quando não existe.
    /// </summary>
    /// <remarks>
    /// Espelhar o gate é o que sustenta a garantia do CA-16/CA-17. Olhar só o cronograma deixaria
    /// o certame de importação externa — que não tem fase de coleta e informa o período — sem data,
    /// e o 422 dele sairia sem a lista de regras reprovadas.
    /// <para>
    /// Não havendo de onde tirar o dia, a recusa nomeia QUAL das duas pendências o rascunho tem, e
    /// não uma ausência genérica — com os MESMOS dois códigos que o gate usaria, e não com códigos
    /// próprios desta consulta: sem fase de coleta,
    /// <c>PeriodoInscricaoObrigatorioSemFaseDeColeta</c>, o que
    /// <see cref="Commands.ProcessosSeletivos.ResolucaoDoPeriodoDeInscricao"/> devolve; com fase de
    /// coleta sem janela, <c>FaseQueColetaInscricaoSemJanela</c>, o que
    /// <see cref="ProcessoSeletivo.AvaliarConformidade"/> aponta e a publicação recusa. Quem
    /// consome isto é o preflight da tela de Revisão, que precisa dizer ao operador o que informar
    /// (issue #1456).
    /// </para>
    /// </remarks>
    private static Result<DateOnly> DiaDeReferenciaLegal(
        ProcessoSeletivo processo,
        DateTimeOffset? periodoInscricaoInformado,
        IResolvedorFusoInstitucional resolvedorFuso)
    {
        FaseCronograma? ancora = processo.FaseQueAncoraOPeriodoDeInscricao();

        if ((ancora?.Inicio ?? periodoInscricaoInformado) is not { } inicio)
        {
            return Result<DateOnly>.Failure(ancora is null
                ? new DomainError(
                    "ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta",
                    "O processo não tem fase do cronograma que colete inscrição, então o período de inscrição precisa ser informado na publicação.")
                : new DomainError(
                    "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                    $"A fase '{ancora.Codigo}' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período."));
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            // Zona irresolvível é defeito de instalação, e os gates de publicação a mapeiam para
            // 500. Recusar aqui como pendência do rascunho viraria 422 — a consulta pediria ao
            // operador que informasse algo que não resolve nada, e esconderia a falha de
            // configuração que ela deveria justamente antecipar.
            throw new InvalidOperationException(fuso.Error!.Message);
        }

        return Result<DateOnly>.Success(
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(inicio, fuso.Value!).DateTime));
    }

}
