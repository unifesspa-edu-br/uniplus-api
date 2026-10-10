namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O contexto de avaliação da finalidade Inscrição na versão FIXADA por uma inscrição ao iniciar —
/// nunca a vigente por relógio (UNI-REQ-0084): as regras recortadas, os termos exigidos dessa
/// finalidade e a data de referência dos fatos congelada na publicação. Reusado por Salvar e por
/// Submeter (issue #1859), que avaliam a mesma versão do mesmo jeito.
/// </summary>
public sealed record ContextoDaVersaoFixada(
    FormularioPortavel Regras,
    IReadOnlyCollection<TermoExigidoFormulario> TermosExigidos,
    DateOnly? DataReferenciaFatos);

public static class ContextoDaVersaoFixadaResolver
{
    public static async Task<Result<ContextoDaVersaoFixada>> ResolverAsync(
        Guid versaoFormularioId,
        Guid processoSeletivoId,
        Domain.Interfaces.IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> resolvida = await VersaoFixadaDaInscricaoResolver
            .ResolverAsync(versaoFormularioId, processoSeletivoId, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (resolvida.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(resolvida.Error!);
        }

        VersaoConfiguracao versao = resolvida.Value!;

        if (!registroCodecs.SabeLer(versao.SchemaVersion))
        {
            return Result<ContextoDaVersaoFixada>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope congelado não está entre as capacidades de leitura reconhecidas pelo codec vivo."));
        }

        Result<EnvelopeReidratado> reidratado = registroCodecs.Reidratar(versao);
        if (reidratado.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(reidratado.Error!);
        }

        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!);
        if (avaliavel.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(avaliavel.Error!);
        }

        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(
            avaliavel.Value!.Definicao,
            Domain.Services.DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, null),
            avaliavel.Value!.Ofertas);

        Result<DateOnly?> dataReferencia = ProjecaoDoFormularioDaVersao.ResolverDataReferenciaFatos(versao);
        if (dataReferencia.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(dataReferencia.Error!);
        }

        IReadOnlyCollection<TermoExigidoFormulario> termos =
            [.. reidratado.Value!.Grafo.TermosExigidos.Where(static t => t.Finalidade == FinalidadeFormulario.Inscricao)];

        return Result<ContextoDaVersaoFixada>.Success(new ContextoDaVersaoFixada(recorte.Regras, termos, dataReferencia.Value));
    }
}
