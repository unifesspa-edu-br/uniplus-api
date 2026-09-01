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
/// Espelha <see cref="ConferenciaDoTipoDeAto"/> em estilo: helper estático compartilhado
/// pelos três handlers que congelam (<c>Publicar</c>, <c>Retificar</c>,
/// <c>FecharRetificacao</c>), chamado uma vez cada, com o mesmo veredicto.
/// </para>
/// <para>
/// <b>Fonte única (CA-16):</b> <see cref="IObrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync"/>
/// + <see cref="AvaliadorConformidadeLegal.Avaliar"/> é exatamente a mesma dupla de
/// chamadas que <c>ObterConformidadeLegalProcessoSeletivoQueryHandler</c> usa — a regra
/// que a consulta pública mostra reprovada é a mesma que bloqueia a transição.
/// </para>
/// </remarks>
internal static class ConferenciaDeConformidadeLegal
{
    public static async Task<Result<ResultadoConformidade>> AvaliarAsync(
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        ProcessoSeletivo processo,
        DateOnly dataReferencia,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);
        ArgumentNullException.ThrowIfNull(processo);

        string tipoProcessoCodigo = processo.TipoProcesso.Codigo;

        IReadOnlyList<ObrigatoriedadeLegal> regrasVigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(tipoProcessoCodigo, dataReferencia, cancellationToken)
            .ConfigureAwait(false);

        // Antes de avaliar: uma regra que referencia cadastro inexistente é aprovada
        // por vacuidade pelo avaliador, que é domínio puro e não consulta cadastro.
        // Publicar sob ela seria publicar sob obrigação que ninguém pode cumprir.
        Result<ConferenciaDeReferenciasDasRegras.RelatorioDeReferencias> referencias = await ConferenciaDeReferenciasDasRegras
            .ConferirAsync(regrasVigentes, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, cancellationToken)
            .ConfigureAwait(false);
        if (referencias.IsFailure)
        {
            return Result<ResultadoConformidade>.Failure(referencias.Error!);
        }

        // O mesmo levantamento alimenta a avaliação: a identidade que o gate usa para
        // casar exigência com regra é a que a conferência acabou de ler, não uma segunda
        // leitura que poderia divergir dela.
        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, tipoProcessoCodigo, regrasVigentes, referencias.Value!.IdentidadeDoTipoDocumentoPorCodigo);

        bool todasAprovadas = resultado.Regras.All(static r => r.Aprovada);
        if (!todasAprovadas)
        {
            IEnumerable<string> reprovadas = resultado.Regras
                .Where(static r => !r.Aprovada)
                .Select(static r => r.RegraCodigo);

            return Result<ResultadoConformidade>.Failure(new DomainError(
                "ProcessoSeletivo.ConformidadeLegalInsuficiente",
                $"Processo não conforme às obrigatoriedades legais vigentes — reprovado(s): {string.Join(", ", reprovadas)}."));
        }

        return Result<ResultadoConformidade>.Success(resultado);
    }
}
