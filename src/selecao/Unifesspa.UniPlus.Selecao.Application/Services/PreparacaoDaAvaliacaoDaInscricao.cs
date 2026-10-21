namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Prepara e faz a avaliação da inscrição fora do envio — no rascunho, na leitura e na consulta do
/// candidato (ADR-0143): mescla o que chegou ao que a inscrição já gravou, apura os derivados do
/// sistema de que a avaliação depende e a entrega à <see cref="AvaliacaoDaInscricao"/>. Nenhuma etapa é
/// dada como concluída, porque o candidato ainda preenche; e o derivado que não se consegue apurar
/// fica indeterminado, sem recusar — quem recusa é o envio.
/// </summary>
public static class PreparacaoDaAvaliacaoDaInscricao
{
    /// <summary>
    /// A avaliação do que a inscrição gravou, na versão em que ela está (o rascunho adotado na memória, sem gravar), como se as respostas
    /// fossem gravadas em <paramref name="instante"/>, no fuso da janela de inscrição — na forma que o
    /// candidato recebe, junto das opções de curso dessa versão.
    /// </summary>
    public static async Task<Result<InscricaoDto>> ProjetarInscricaoGravadaAsync(
        Inscricao inscricao,
        Domain.Interfaces.ICertameDivulgadoRepository certameDivulgadoRepository,
        Domain.Interfaces.IProcessoSeletivoRepository processoSeletivoRepository,
        Domain.Interfaces.IDocumentoDaInscricaoRepository documentoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IConferenciaEnderecoGeo conferenciaEnderecoGeo,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(documentoRepository);

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<InscricaoDto>.Failure(fuso.Error!);
        }

        Result adotada = await VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<InscricaoDto>.Failure(adotada.Error!);
        }

        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, inscricao.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs,
            inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId, cancellationToken)
            .ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<InscricaoDto>.Failure(contexto.Error!);
        }

        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = await AvaliarAsync(
            inscricao,
            contexto.Value!,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(instante, fuso.Value!),
            conferenciaEnderecoGeo,
            documentoRepository,
            cancellationToken).ConfigureAwait(false);
        if (avaliacao.IsFailure)
        {
            return Result<InscricaoDto>.Failure(avaliacao.Error!);
        }

        Result<AvaliacaoDaInscricaoDto> projetada = ProjecaoDaAvaliacaoDaInscricao.Projetar(
            avaliacao.Value!, contexto.Value!, inscricao.ProcessoSeletivoId, enderecoNoAcervo);
        return projetada.IsFailure
            ? Result<InscricaoDto>.Failure(projetada.Error!)
            : Result<InscricaoDto>.Success(InscricaoDto.De(inscricao, projetada.Value!, contexto.Value!.Admitidas.NumeroDeOpcoesDeCurso, contexto.Value!.OfertasDeCurso));
    }

    /// <param name="respostasRecebidas">As respostas que chegaram; o campo ausente vale o que já está gravado.</param>
    /// <param name="gruposRecebidos">As ocorrências que chegaram, por grupo; o grupo ausente vale o que já está gravado.</param>
    /// <param name="envio">O instante em que as respostas seriam gravadas, de que se contam os limites de data relativos a ele.</param>
    public static async Task<Result<ResultadoDaAvaliacaoDaInscricao>> AvaliarAsync(
        Inscricao inscricao,
        ContextoDaVersaoFixada contexto,
        IReadOnlyDictionary<string, JsonElement> respostasRecebidas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> gruposRecebidos,
        EnvioDasRespostas envio,
        IConferenciaEnderecoGeo conferenciaEnderecoGeo,
        Domain.Interfaces.IDocumentoDaInscricaoRepository documentoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(respostasRecebidas);
        ArgumentNullException.ThrowIfNull(gruposRecebidos);
        ArgumentNullException.ThrowIfNull(conferenciaEnderecoGeo);
        ArgumentNullException.ThrowIfNull(documentoRepository);

        IReadOnlyDictionary<string, JsonElement> respostas = inscricao.RespostasAposMesclar(respostasRecebidas);
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos = InscricaoRequestMapper.ParaAvaliacao(inscricao.Grupos);
        foreach ((string codigo, IReadOnlyList<OcorrenciaRespondida> ocorrencias) in gruposRecebidos)
        {
            grupos[codigo] = ocorrencias;
        }

        IReadOnlyDictionary<string, FatoResolvido> derivados = await DerivadosDoSistemaInscricao.ApurarAsync(
            respostas,
            contexto.DataReferenciaFatos,
            conferenciaEnderecoGeo,
            AvaliacaoDaInscricao.DerivadosDoSistemaCitados(contexto.Configuracao),
            cancellationToken).ConfigureAwait(false);

        return AvaliacaoDaInscricaoPorOpcao.Avaliar(
            contexto,
            new EntradaDaAvaliacaoDaInscricao(respostas, grupos, derivados, new HashSet<string>(StringComparer.Ordinal), envio,
                await DocumentosEnviadosDaInscricao.ApresentacoesAsync(documentoRepository, inscricao.Id, cancellationToken).ConfigureAwait(false),
                NaFaseDaInscricao: true));
    }
}
