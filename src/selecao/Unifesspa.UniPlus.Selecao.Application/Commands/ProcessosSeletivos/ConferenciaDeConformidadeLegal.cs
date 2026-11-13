namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resolve as <see cref="ObrigatoriedadeLegal"/> vigentes para o tipo do processo na
/// data de corte (Story #852 §3.1: <c>DadosEdital.PeriodoInscricaoInicio</c>), avalia
/// contra o agregado (<see cref="AvaliadorConformidadeLegal"/>) e recusa a transição
/// ANTES da canonicalização se qualquer regra reprovar — o mesmo momento em que o gate
/// estrutural (<c>PendenciaDeConformidade</c>) já recusa (ADR-0109 D5).
/// </summary>
/// <remarks>
/// <para>
/// Serviço compartilhado pela conferência do congelamento das três operações (<c>Publicar</c>,
/// <c>Retificar</c> e <c>FecharRetificacao</c>), chamado uma vez em cada, com o mesmo veredicto.
/// </para>
/// <para>
/// <b>Fonte única (CA-16):</b> <see cref="IObrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync"/>
/// + <see cref="AvaliadorConformidadeLegal.Avaliar"/> é exatamente a mesma dupla de
/// chamadas que <c>ObterConformidadeLegalProcessoSeletivoQueryHandler</c> usa — a regra
/// que a consulta pública mostra reprovada é a mesma que bloqueia a transição.
/// </para>
/// </remarks>
public sealed class ConferenciaDeConformidadeLegal(
    IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
    IModalidadeReader modalidadeReader,
    ITipoDocumentoReader tipoDocumentoReader,
    ITipoEtapaReader tipoEtapaReader,
    ITipoDeficienciaReader tipoDeficienciaReader,
    IRegraCatalogoReader regraCatalogoReader)
{
    // O mesmo levantamento das referências alimenta a avaliação: a identidade que o gate usa para casar exigência com
    // regra é a que a conferência acabou de ler, não uma segunda leitura que poderia divergir dela.
    internal async Task<Result<ResultadoConformidade>> AvaliarAsync(
        ProcessoSeletivo processo,
        DateOnly dataReferencia,
        FatosDeModalidade fatosDeModalidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        string tipoProcessoCodigo = processo.TipoProcesso.Codigo;
        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(tipoProcessoCodigo, dataReferencia, cancellationToken)
            .ConfigureAwait(false);
        Result<ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias> referencias = await ConferirAsReferenciasAsync(regrasVigentes, cancellationToken)
            .ConfigureAwait(false);

        return referencias.IsFailure
            ? Result<ResultadoConformidade>.Failure(referencias.Error!)
            : SomenteAprovadas(AvaliadorConformidadeLegal.Avaliar(
                processo, tipoProcessoCodigo, regrasVigentes, referencias.Value!.Identidades, fatosDeModalidade));
    }

    // Antes de avaliar: uma regra que referencia cadastro inexistente é aprovada por vacuidade pelo avaliador, que é
    // domínio puro e não consulta cadastro. Publicar sob ela seria publicar sob obrigação que ninguém pode cumprir.
    private Task<Result<ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias>> ConferirAsReferenciasAsync(
        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes, CancellationToken cancellationToken) =>
        ConferenciaDeReferenciasDasRegras.ConferirAsync(
            regrasVigentes, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, cancellationToken);

    private static Result<ResultadoConformidade> SomenteAprovadas(ResultadoConformidade resultado)
    {
        string[] reprovadas = [.. resultado.Regras.Where(static r => !r.Aprovada).Select(static r => r.RegraCodigo)];

        return reprovadas.Length > 0
            ? Result<ResultadoConformidade>.Failure(new DomainError(
                "ProcessoSeletivo.ConformidadeLegalInsuficiente",
                $"Processo não conforme às obrigatoriedades legais vigentes — reprovado(s): {string.Join(", ", reprovadas)}."))
            : Result<ResultadoConformidade>.Success(resultado);
    }
}
