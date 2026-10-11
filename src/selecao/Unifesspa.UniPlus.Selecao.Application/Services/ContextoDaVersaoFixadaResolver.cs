namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O contexto de avaliação da finalidade Inscrição na versão FIXADA por uma inscrição ao iniciar —
/// nunca a vigente por relógio (UNI-REQ-0084): os termos exigidos dessa finalidade, a data de
/// referência dos fatos congelada na publicação, a oferta de valores de cada campo, o que a versão
/// admite como resposta, a configuração inteira que a avaliação da inscrição lê (ADR-0143) e o ato que
/// publicou a versão, de que é o endereço dos modelos de documento no acervo. Reusado por Salvar, por
/// Submeter e pela consulta do candidato, que avaliam a mesma versão do mesmo jeito.
/// </summary>
public sealed record ContextoDaVersaoFixada(
    IReadOnlyCollection<TermoExigidoFormulario> TermosExigidos,
    DateOnly? DataReferenciaFatos,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    RespostasAdmitidas Admitidas,
    ConfiguracaoAvaliavel Configuracao,
    Guid AtoCriadorId);

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

        Result<DateOnly?> dataReferencia = ProjecaoDoFormularioDaVersao.ResolverDataReferenciaFatos(versao);
        if (dataReferencia.IsFailure)
        {
            return Result<ContextoDaVersaoFixada>.Failure(dataReferencia.Error!);
        }

        IReadOnlyCollection<TermoExigidoFormulario> termos =
            [.. reidratado.Value!.Grafo.TermosExigidos.Where(static t => t.Finalidade == FinalidadeFormulario.Inscricao)];

        return Result<ContextoDaVersaoFixada>.Success(new ContextoDaVersaoFixada(
            termos, dataReferencia.Value, avaliavel.Value!.Ofertas, Admitidas(reidratado.Value!.Grafo),
            ConfiguracaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!.Grafo, avaliavel.Value!),
            versao.AtoCriadorId));
    }

    public static RespostasAdmitidas Admitidas(GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return new RespostasAdmitidas(
            DefinicoesDasRespostasDaInscricao(grafo),
            grafo.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId).ToHashSet());
    }

    private static Dictionary<string, DefinicaoDaResposta> DefinicoesDasRespostasDaInscricao(GrafoConfiguracao grafo)
    {
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal);
        IEnumerable<FatoColetado> coletados = grafo.FatosColetados
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)
            .Concat(grafo.GruposColetados
                .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
                .SelectMany(static g => g.Subitens));
        foreach (FatoColetado fato in coletados)
        {
            definicoes.TryAdd(fato.FatoCodigo, DefinicaoDaResposta.De(fato.TipoRenderizacao, fato.ClassificacaoProtecao, fato.Formato));
        }

        return definicoes;
    }
}
