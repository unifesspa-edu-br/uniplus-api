namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
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

        ArquivosDaInscricao arquivos = await ArquivosDaInscricao.CarregarAsync(documentoRepository, inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = await AvaliarAsync(
            inscricao,
            contexto.Value!,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(instante, fuso.Value!),
            conferenciaEnderecoGeo,
            arquivos,
            cancellationToken).ConfigureAwait(false);
        if (avaliacao.IsFailure)
        {
            return Result<InscricaoDto>.Failure(avaliacao.Error!);
        }

        Result<AvaliacaoDaInscricaoDto> projetada = ProjecaoDaAvaliacaoDaInscricao.Projetar(
            avaliacao.Value!, contexto.Value!, inscricao.ProcessoSeletivoId, enderecoNoAcervo,
            arquivos.ParaOCandidato());
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
        ArquivosDaInscricao arquivos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(respostasRecebidas);
        ArgumentNullException.ThrowIfNull(gruposRecebidos);
        ArgumentNullException.ThrowIfNull(conferenciaEnderecoGeo);
        ArgumentNullException.ThrowIfNull(arquivos);

        IReadOnlyDictionary<string, JsonElement> respostas = inscricao.RespostasAposMesclar(respostasRecebidas);
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos = InscricaoRequestMapper.ParaAvaliacao(inscricao.Grupos);
        foreach ((string codigo, IReadOnlyList<OcorrenciaRespondida> ocorrencias) in gruposRecebidos)
        {
            grupos[codigo] = ocorrencias;
        }

        // A inscrição enviada diz da residência o que o envio conferiu no Geo: a consulta não reconfere, e o veredito não muda
        // com o Geo. Só o endereço residencial novo no corpo é apurado de novo.
        IReadOnlySet<string> citados = AvaliacaoDaInscricao.DerivadosDoSistemaCitados(contexto.Configuracao);
        bool residenciaDoEnvio = !inscricao.EhRascunho && !TrazEnderecoResidencial(respostasRecebidas);
        Dictionary<string, FatoResolvido> derivados = new(
            await DerivadosDoSistemaInscricao.ApurarAsync(
                respostas,
                contexto.DataReferenciaFatos,
                conferenciaEnderecoGeo,
                residenciaDoEnvio ? citados.Except(DerivadosDoSistemaInscricao.FatosDaResidencia).ToHashSet(StringComparer.Ordinal) : citados,
                cancellationToken).ConfigureAwait(false),
            StringComparer.Ordinal);
        if (residenciaDoEnvio)
        {
            foreach ((string fato, FatoResolvido resolvido) in DerivadosDoSistemaInscricao.ResidenciaGravada(inscricao.Derivados, citados))
            {
                derivados[fato] = resolvido;
            }
        }

        return AvaliacaoDaInscricaoPorOpcao.Avaliar(
            contexto,
            EntradaDaAvaliacaoDaInscricao.Montar(
                DerivadosDoSistemaInscricao.ComOsDerivados(contexto.Admitidas.Definicoes),
                respostas, grupos, derivados, new HashSet<string>(StringComparer.Ordinal), envio,
                arquivos.ApresentacoesVerificadas(),
                naFaseDaInscricao: true));
    }

    private static bool TrazEnderecoResidencial(IReadOnlyDictionary<string, JsonElement> respostasRecebidas) =>
        respostasRecebidas.TryGetValue(DerivadosDoSistemaInscricao.EnderecoResidencial, out JsonElement endereco)
        && endereco.ValueKind != JsonValueKind.Null;
}
