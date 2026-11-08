namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Wolverine.Attributes;

/// <summary>
/// A inscrição enviada na visão de quem opera o processo, projetada pela permissão da classe de cada dado e de cada
/// arquivo (ADR-0153). A leitura tem dois níveis: o acesso ao resource, decidido antes da busca, e a visibilidade de cada
/// item, decidida antes da montagem da resposta.
/// </summary>
/// <remarks>
/// <para>
/// Nível 1: o handler lê só o vínculo da inscrição, sem decifrar nada. A inscrição que não existe, a de outro processo e
/// a em rascunho respondem 404: sem a permissão da rota na unidade dona, grava a negativa sem o id do resource; com ela,
/// não grava nada. Com o vínculo confirmado, decide o acesso à inscrição, com os dois resultados registrados; sem permissão responde 404 e o conteúdo
/// não é carregado.
/// </para>
/// <para>
/// Nível 2: o handler pede as decisões de todas as classes de dado e de todas as de arquivo, sem
/// olhar o conteúdo, e registra só as permitidas: o registro significa acesso autorizado à classe naquela inscrição, e não
/// dado entregue. Quem não pode ver um item o recebe omitido, sem indicação; o identificador mascarado
/// só indica que pode ser revelado a quem também tem a permissão de revelar. A requisição só segue depois de gravados os registros.
/// </para>
/// </remarks>
public static class ObterInscricaoAdministrativaQueryHandler
{
    private static readonly (NaturezaDoItem, ClassificacaoDeProtecao)[] ClassesPedidas =
    [
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Publico),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Interno),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Pessoal),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Identificador),
        (NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Sensivel),
    ];

    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c> e os repositórios
    /// leem o <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<InscricaoAdministrativaDto>> Handle(
        ObterInscricaoAdministrativaQuery query,
        IVerificadorDeAcesso verificador,
        ProjetorDeItens projetor,
        IPortaDePermissaoPorPar porta,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(porta);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        ConferenciaDoVinculo conferencia = await AcessoAInscricao
            .ConferirVinculoAsync(processoRepository, inscricaoRepository, query.ProcessoSeletivoId, query.InscricaoId, cancellationToken)
            .ConfigureAwait(false);
        if (conferencia is not Vinculo conferido)
        {
            await AcessoAInscricao
                .RegistrarNegativaDoInexistenteAsync(
                    verificador, conferencia, query.ProcessoSeletivoId, [PermissaoSelecao.InscricoesConsultar.Requisito], cancellationToken)
                .ConfigureAwait(false);
            return Result<InscricaoAdministrativaDto>.Failure(AcessoAInscricao.NaoEncontradaNoProcesso);
        }

        Guid unidade = conferido.UnidadeId;
        VinculoDaInscricao vinculo = conferido.Inscricao;
        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(unidade, query.ProcessoSeletivoId, query.InscricaoId);
        if (await AcessoAInscricao
            .DecidirAsync(verificador, conferido, PermissaoSelecao.InscricoesConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false) is Recusada recusada)
        {
            return Result<InscricaoAdministrativaDto>.Failure(AcessoAInscricao.Erro(recusada, AcessoAInscricao.NaoEncontradaNoProcesso));
        }

        Result<VersaoConfiguracao> versao = await VersaoFixadaDaInscricaoResolver
            .ResolverAsync(vinculo.VersaoFormularioId, vinculo.ProcessoSeletivoId, processoRepository, cancellationToken)
            .ConfigureAwait(false);
        Result<EnvelopeReidratado> envelope = versao.IsFailure
            ? Result<EnvelopeReidratado>.Failure(versao.Error!)
            : ContextoDaVersaoFixadaResolver.Reidratar(versao.Value!, registroCodecs);
        if (envelope.IsFailure)
        {
            return Result<InscricaoAdministrativaDto>.Failure(envelope.Error!);
        }

        DecisoesDaProjecao decisoes = await projetor
            .DecidirAsync(porta, resource, ClassesPedidas, cancellationToken).ConfigureAwait(false);
        bool podeRevelar = decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador)
            && await verificador
                .PodeAsync(PermissaoSelecao.DadosIdentificadoresRevelar.Requisito, resource, cancellationToken)
                .ConfigureAwait(false) == ResultadoDoAcesso.Permitido;
        await projetor.RegistrarAsync(decisoes, cancellationToken).ConfigureAwait(false);

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(query.InscricaoId, cancellationToken).ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<InscricaoAdministrativaDto>.Failure(AcessoAInscricao.NaoEncontradaNoProcesso);
        }

        FatosDaVersao fatos = FatosDaVersao.De(envelope.Value!);
        IReadOnlyList<ArquivoAdministrativoDto> arquivos = await ArquivosDaInscricaoAsync(
            inscricao.Id, envelope.Value!, documentoRepository, cancellationToken).ConfigureAwait(false);

        DateTimeOffset? submetidaEm = await FixoAsync(
            decisoes, nameof(InscricaoAdministrativaDto.SubmetidaEm),
            () => PrimeiroEnvioAsync(inscricao.Id, inscricaoRepository, cancellationToken)).ConfigureAwait(false);

        return Result<InscricaoAdministrativaDto>.Success(new InscricaoAdministrativaDto(
            inscricao.Id,
            new ProcessoDaInscricaoDto(query.ProcessoSeletivoId, conferido.ProcessoNome),
            Fixo<SituacaoInscricao>(decisoes, nameof(InscricaoAdministrativaDto.Situacao), inscricao.Situacao),
            decisoes.PermitePropriedade<InscricaoAdministrativaDto>(nameof(InscricaoAdministrativaDto.Ofertas))
                ? OfertasEscolhidas.Montar(
                    inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId,
                    OfertasEscolhidas.NomesDe(ContextoDaVersaoFixadaResolver.OfertasDaVersao(envelope.Value!.Grafo)))
                : null,
            Fixo(decisoes, nameof(InscricaoAdministrativaDto.NumeroInscricao), inscricao.NumeroInscricao),
            submetidaEm,
            Fixo<DateTimeOffset>(decisoes, nameof(InscricaoAdministrativaDto.CanceladaEm), inscricao.CanceladaEm),
            Fixo(decisoes, nameof(InscricaoAdministrativaDto.MotivoDoCancelamento), inscricao.MotivoDoCancelamento),
            ItensDaInscricao.Permitidas(ItensDaInscricao.Secoes(inscricao.Respostas, inscricao.Grupos, fatos, podeRevelar), decisoes),
            ItensDaInscricao.Visiveis(ItensDaInscricao.Derivados(inscricao.Derivados, fatos), decisoes),
            [.. arquivos.Where(decisoes.ItemSai)],
            await PodeEmitirComprovanteAsync(verificador, resource, cancellationToken).ConfigureAwait(false),
            await NumeroDoUltimoComprovanteAsync(inscricao.Id, inscricaoRepository, cancellationToken).ConfigureAwait(false)));
    }

    private static async Task<bool> PodeEmitirComprovanteAsync(
        IVerificadorDeAcesso verificador, ResourceContext resource, CancellationToken cancellationToken) =>
        await verificador.PodeAsync(PermissaoSelecao.InscricoesEmitirComprovante.Requisito, resource, cancellationToken)
            .ConfigureAwait(false) == ResultadoDoAcesso.Permitido;

    /// <summary>
    /// O número do último comprovante, que é a quantidade emitida, lida só por projeção, sem decifrar nenhum; nulo sem comprovante.
    /// </summary>
    private static async Task<int?> NumeroDoUltimoComprovanteAsync(
        Guid inscricaoId, IInscricaoRepository inscricaoRepository, CancellationToken cancellationToken)
    {
        IReadOnlyList<EmissaoDoComprovante> emissoes = await inscricaoRepository
            .ObterEmissoesAsync([inscricaoId], cancellationToken).ConfigureAwait(false);

        return ComprovanteNaPosicao.NumeroDoUltimo(emissoes.Count);
    }

    /// <summary>
    /// Como <c>Fixo</c>, para o valor que se lê do repositório: a leitura só acontece quando a propriedade é permitida.
    /// </summary>
    private static async Task<T?> FixoAsync<T>(DecisoesDaProjecao decisoes, string propriedade, Func<Task<T?>> ler)
        where T : struct => decisoes.PermitePropriedade<InscricaoAdministrativaDto>(propriedade) ? await ler().ConfigureAwait(false) : null;

    private static async Task<DateTimeOffset?> PrimeiroEnvioAsync(
        Guid inscricaoId, IInscricaoRepository inscricaoRepository, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, DateTimeOffset> envios = await inscricaoRepository
            .ObterPrimeirosEnviosAsync([inscricaoId], cancellationToken).ConfigureAwait(false);

        return envios.TryGetValue(inscricaoId, out DateTimeOffset primeiro) ? primeiro : null;
    }

    private static T? Fixo<T>(DecisoesDaProjecao decisoes, string propriedade, T? valor)
        where T : struct => decisoes.PermitePropriedade<InscricaoAdministrativaDto>(propriedade) ? valor : null;

    private static string? Fixo(DecisoesDaProjecao decisoes, string propriedade, string? valor) =>
        decisoes.PermitePropriedade<InscricaoAdministrativaDto>(propriedade) ? valor : null;

    /// <summary>
    /// Os metadados dos arquivos confirmados, cada um com a classificação e a hipótese legal que a exigência congelou na
    /// versão da inscrição. A exigência que a versão não tem deixa a classificação sem resolver, e o item vale como sensível
    /// sem base legal.
    /// </summary>
    private static async Task<IReadOnlyList<ArquivoAdministrativoDto>> ArquivosDaInscricaoAsync(
        Guid inscricaoId, EnvelopeReidratado envelope, IDocumentoDaInscricaoRepository documentoRepository, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApresentacaoDeDocumento> apresentacoes = await documentoRepository
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ArquivoDaApresentacao> arquivos = await documentoRepository
            .ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, ApresentacaoDeDocumento> porId = apresentacoes.ToDictionary(static a => a.Id);

        return
        [
            .. arquivos
                .Where(a => a.ConfirmadoEm is not null && porId.ContainsKey(a.ApresentacaoId))
                .OrderBy(static a => a.ConfirmadoEm)
                .Select(a =>
                {
                    ApresentacaoDeDocumento apresentacao = porId[a.ApresentacaoId];
                    DocumentoExigido? exigencia = envelope.Grafo.DocumentosExigidos.FirstOrDefault(d => d.Id == apresentacao.ExigenciaId);
                    return new ArquivoAdministrativoDto(
                        exigencia?.ClassificacaoProtecao ?? ClassificacaoDeProtecao.Nenhuma,
                        string.IsNullOrWhiteSpace(exigencia?.HipoteseLegal) ? null : exigencia.HipoteseLegal)
                    {
                        ArquivoId = a.Id,
                        ExigenciaId = apresentacao.ExigenciaId,
                        OcorrenciaId = apresentacao.OcorrenciaId,
                        NomeDoDocumento = apresentacao.NomeDoDocumento,
                        TipoDeMidia = a.TipoDeMidia,
                        Situacao = a.Situacao,
                        TamanhoBytes = a.TamanhoBytes,
                        HashSha256 = a.HashSha256,
                        ConfirmadoEm = a.ConfirmadoEm!.Value,
                    };
                }),
        ];
    }
}
