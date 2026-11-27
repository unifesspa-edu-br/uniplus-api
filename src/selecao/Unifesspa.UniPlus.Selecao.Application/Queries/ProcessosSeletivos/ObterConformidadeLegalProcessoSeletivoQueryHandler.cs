namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>
/// Handler da <see cref="ObterConformidadeLegalProcessoSeletivoQuery"/>: avalia pelo mesmo
/// <see cref="ConferenciaDeConformidadeLegal.LevantarAsync"/> que o gate de congelamento —
/// <see cref="IObrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync"/> +
/// <see cref="AvaliadorConformidadeLegal.Avaliar"/> — para que a leitura pública e a
/// transição nunca divirjam (CA-16).
/// </summary>
/// <remarks>
/// Distingue 404 (processo inexistente) de 422 (não há data de referência de onde partir) —
/// mesmo contrato de erro de <see cref="ObterFormularioApresentadoQueryHandler"/>. Traduzir a
/// segunda condição em 404 diria que o processo não existe e esconderia a pendência que a
/// consulta deveria justamente antecipar.
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
        ConferenciaDeConformidadeLegal conferencia,
        IResolvedorFusoInstitucional resolvedorFuso,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return processo is null
            ? Result<ConformidadeLegalProcessoSeletivoDto>.Failure(AcessoAoProcesso.NaoEncontrado())
            : await ConformidadeDoProcessoAsync(processo, query, conferencia, resolvedorFuso, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ConformidadeLegalProcessoSeletivoDto>> ConformidadeDoProcessoAsync(
        ProcessoSeletivo processo,
        ObterConformidadeLegalProcessoSeletivoQuery query,
        ConferenciaDeConformidadeLegal conferenciaDeConformidadeLegal,
        IResolvedorFusoInstitucional resolvedorFuso,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        Result<DateOnly> dia = DiaDaConsulta(processo, query, resolvedorFuso);
        if (dia.IsFailure)
        {
            return Result<ConformidadeLegalProcessoSeletivoDto>.Failure(dia.Error!);
        }

        FatosDeModalidade fatosDeModalidade = VocabularioDeFatos.ComValoresDeModalidade(
            await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false));
        ConformidadeLegalLevantada levantada = await conferenciaDeConformidadeLegal
            .LevantarAsync(processo, dia.Value!, fatosDeModalidade, cancellationToken)
            .ConfigureAwait(false);

        return Result<ConformidadeLegalProcessoSeletivoDto>.Success(new ConformidadeLegalProcessoSeletivoDto(
            processo.Id, dia.Value!, RegrasProjetadas(levantada), levantada.Resultado.Avisos));
    }

    private static RegraAvaliadaDto[] RegrasProjetadas(ConformidadeLegalLevantada levantada) =>
        [.. levantada.Resultado.Regras.Select(r => RegraProjetada(r, levantada.MotivoDaInavaliabilidade(r.RegraId)))];

    /// <summary>
    /// A regra inavaliável aparece reprovada, com o motivo da inavaliabilidade no lugar do motivo do avaliador.
    /// </summary>
    private static RegraAvaliadaDto RegraProjetada(RegraAvaliada regra, string? motivoDaInavaliabilidade) =>
        new(
            regra.RegraId,
            regra.RegraCodigo,
            CodigoComRotulo<CategoriaObrigatoriedade>.De(regra.Categoria),
            regra.TipoProcessoCodigoAvaliado,
            regra.Predicado,
            regra.Aprovada && motivoDaInavaliabilidade is null,
            motivoDaInavaliabilidade ?? regra.Motivo,
            regra.BaseLegal,
            regra.AtoNormativoUrl,
            regra.PortariaInterna,
            regra.DescricaoHumana,
            regra.VigenciaInicio,
            regra.VigenciaFim,
            regra.Hash);

    /// <summary>
    /// Sem data explícita, a consulta responde pelo mesmo dia que o gate de publicação usaria.
    /// </summary>
    /// <remarks>
    /// Derivar o dia aqui, e não deixar o chamador informar, é o que impede o preflight de
    /// dizer "conforme" numa data que o comando contradiz.
    /// </remarks>
    private static Result<DateOnly> DiaDaConsulta(
        ProcessoSeletivo processo,
        ObterConformidadeLegalProcessoSeletivoQuery query,
        IResolvedorFusoInstitucional resolvedorFuso) =>
        query.DataReferencia is { } informada
            ? Result<DateOnly>.Success(informada)
            : DiaDeReferenciaLegal(processo, query.PeriodoInscricaoInformado, resolvedorFuso);

    /// <summary>
    /// O dia que o gate de publicação usaria, pela MESMA resolução dele: a janela da fase que
    /// coleta inscrição quando ela existe, o período informado no ato quando não existe.
    /// </summary>
    /// <remarks>
    /// Espelhar o gate é o que sustenta a garantia do CA-16/CA-17. Olhar só o cronograma deixaria
    /// o certame de importação externa — que não tem fase de coleta e informa o período — sem data,
    /// e o 422 dele sairia sem a lista de regras reprovadas.
    /// </remarks>
    private static Result<DateOnly> DiaDeReferenciaLegal(
        ProcessoSeletivo processo,
        DateTimeOffset? periodoInscricaoInformado,
        IResolvedorFusoInstitucional resolvedorFuso)
    {
        Result<DateTimeOffset> inicio = InicioDoPeriodoDeInscricao(processo, periodoInscricaoInformado);

        return inicio.IsFailure
            ? Result<DateOnly>.Failure(inicio.Error!)
            : Result<DateOnly>.Success(DiaNoFusoInstitucional(inicio.Value!, resolvedorFuso));
    }

    private static Result<DateTimeOffset> InicioDoPeriodoDeInscricao(
        ProcessoSeletivo processo, DateTimeOffset? periodoInscricaoInformado)
    {
        FaseCronograma? ancora = ReferenciaTemporalDosFatos.FaseQueAncoraOPeriodoDeInscricao(processo.Declaracao());

        return ancora is null
            ? InicioSemFaseDeColeta(processo.OrigemCandidatos, periodoInscricaoInformado)
            : InicioDaJanelaDaFase(ancora);
    }

    /// <summary>
    /// Não havendo de onde tirar o dia, a recusa nomeia QUAL das pendências o rascunho tem, e
    /// não uma ausência genérica — com os MESMOS códigos que o gate usaria, e na MESMA ordem em
    /// que o gate os emitiria.
    /// </summary>
    /// <remarks>
    /// A ordem não é detalhe de apresentação: quem coleta inscrição pelo sistema e não tem fase
    /// que a colete precisa CRIAR A FASE, não informar período no ato.
    /// <see cref="ProcessoSeletivo.AvaliarConformidade"/>, que a publicação consulta primeiro,
    /// recusa isso com <c>InscricaoPropriaSemFaseDeColeta</c> antes de a resolução do período ser
    /// sequer consultada, e mandar esse operador para o campo de período o levaria ao lugar
    /// errado. Só então, para o certame de origem importada, vem
    /// <c>PeriodoInscricaoObrigatorioSemFaseDeColeta</c>, o que
    /// <see cref="Commands.ProcessosSeletivos.ResolucaoDoPeriodoDeInscricao"/> devolve. Quem
    /// consome isto é o preflight da tela de Revisão, que precisa dizer ao operador o que
    /// informar — e a recusa que sai primeiro é a que o orienta.
    /// </remarks>
    private static Result<DateTimeOffset> InicioSemFaseDeColeta(
        OrigemCandidatos origem, DateTimeOffset? periodoInscricaoInformado)
    {
        if (origem == OrigemCandidatos.InscricaoPropria)
        {
            return Result<DateTimeOffset>.Failure(new DomainError(
                "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
                "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição."));
        }

        return periodoInscricaoInformado is { } informado
            ? Result<DateTimeOffset>.Success(informado)
            : Result<DateTimeOffset>.Failure(new DomainError(
                "ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta",
                "O processo não tem fase do cronograma que colete inscrição, então o período de inscrição precisa ser informado na publicação."));
    }

    /// <summary>
    /// Janela MEIO-ABERTA também recusa: o predicado do gate é <c>Inicio is null || Fim is null</c>
    /// (<c>ProcessoSeletivo.FaseQueColetaInscricaoSemJanela</c>).
    /// </summary>
    /// <remarks>
    /// Olhar só o <c>Inicio</c> deixaria a consulta responder 200 para um rascunho que a
    /// publicação recusa — a divergência exata que este handler existe para não ter.
    /// </remarks>
    private static Result<DateTimeOffset> InicioDaJanelaDaFase(FaseCronograma fase) =>
        fase.Inicio is { } inicio && fase.Fim is not null
            ? Result<DateTimeOffset>.Success(inicio)
            : Result<DateTimeOffset>.Failure(
                RecusasDaJanelaDaInscricao.FaseQueColetaInscricaoSemJanela(fase.Citacao));

    /// <summary>
    /// Zona irresolvível é defeito de instalação, e os gates de publicação a mapeiam para 500.
    /// </summary>
    /// <remarks>
    /// Recusar aqui como pendência do rascunho viraria 422 — a consulta pediria ao operador que
    /// informasse algo que não resolve nada, e esconderia a falha de configuração que ela
    /// deveria justamente antecipar.
    /// </remarks>
    /// <exception cref="InvalidOperationException">O fuso institucional não pôde ser resolvido.</exception>
    private static DateOnly DiaNoFusoInstitucional(DateTimeOffset instante, IResolvedorFusoInstitucional resolvedorFuso)
    {
        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            throw new InvalidOperationException(fuso.Error!.Message);
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fuso.Value!).DateTime);
    }
}
