namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
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
/// a em rascunho respondem 404 sem decisão gravada, porque não há resource sobre o qual decidir. Com o vínculo
/// confirmado, decide o acesso à inscrição, com os dois resultados registrados; sem permissão responde 404 e o conteúdo
/// não é carregado.
/// </para>
/// <para>
/// Nível 2: o handler pede as decisões de todas as classes de dado, menos o identificador, e de todas as de arquivo, sem
/// olhar o conteúdo, e registra só as permitidas: o registro significa acesso autorizado à classe naquela inscrição, e não
/// dado entregue. Quem não pode ver um item o recebe omitido, sem indicação. A requisição só segue depois de gravados os
/// registros.
/// </para>
/// </remarks>
public static class ObterInscricaoAdministrativaQueryHandler
{
    private static readonly (NaturezaDoItem, ClassificacaoDeProtecao)[] ClassesPedidas =
    [
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno),
        (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal),
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
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(projetor);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Guid? unidade = await processoRepository
            .ObterUnidadeAdministradoraAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        VinculoDaInscricao? vinculo = await inscricaoRepository
            .ObterVinculoAsync(query.InscricaoId, cancellationToken).ConfigureAwait(false);
        if (unidade is null
            || vinculo is null
            || vinculo.ProcessoSeletivoId != query.ProcessoSeletivoId
            || vinculo.Situacao == SituacaoInscricao.Rascunho)
        {
            return Result<InscricaoAdministrativaDto>.Failure(InscricaoNaoEncontrada);
        }

        ResultadoDoAcesso acesso = await AcessoAoProcesso
            .VerificarNaUnidadeAsync(
                verificador, unidade, query.ProcessoSeletivoId, PermissaoSelecao.InscricoesConsultar,
                ResourceTiposSelecao.Inscricao, ClassificacaoDeProtecao.Pessoal, cancellationToken,
                query.InscricaoId, inscricaoId: query.InscricaoId)
            .ConfigureAwait(false);
        if (acesso is ResultadoDoAcesso.IdentidadeIncompleta or ResultadoDoAcesso.Impedido)
        {
            return Result<InscricaoAdministrativaDto>.Failure(AcessoAoProcesso.Recusa(acesso));
        }

        if (acesso != ResultadoDoAcesso.Permitido)
        {
            return Result<InscricaoAdministrativaDto>.Failure(InscricaoNaoEncontrada);
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

        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(unidade, query.ProcessoSeletivoId, query.InscricaoId)!;
        DecisoesDaProjecao decisoes = await projetor
            .DecidirAsync(PortaDeProjecaoDaSelecao.Instancia, resource, ClassesPedidas, cancellationToken).ConfigureAwait(false);
        await projetor.RegistrarAsync(decisoes, cancellationToken).ConfigureAwait(false);

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(query.InscricaoId, cancellationToken).ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<InscricaoAdministrativaDto>.Failure(InscricaoNaoEncontrada);
        }

        bool podeRevelar = decisoes.Permite(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal)
            && await AcessoAoProcesso
                .PodeNaUnidadeAsync(
                    verificador, unidade, query.ProcessoSeletivoId, PermissaoSelecao.DadosIdentificadoresRevelar,
                    ResourceTiposSelecao.Inscricao, cancellationToken)
                .ConfigureAwait(false) == ResultadoDoAcesso.Permitido;

        FatosDaVersao fatos = FatosDaVersao.De(envelope.Value!);
        IReadOnlyList<ArquivoAdministrativoDto> arquivos = await ArquivosDaInscricaoAsync(
            inscricao.Id, envelope.Value!, documentoRepository, cancellationToken).ConfigureAwait(false);

        DateTimeOffset? submetidaEm = await FixoAsync(
            decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno,
            () => PrimeiroEnvioAsync(inscricao.Id, inscricaoRepository, cancellationToken)).ConfigureAwait(false);

        return Result<InscricaoAdministrativaDto>.Success(new InscricaoAdministrativaDto(
            Fixo<Guid>(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, inscricao.Id),
            Fixo<SituacaoInscricao>(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, inscricao.Situacao),
            Fixo<Guid>(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, inscricao.OfertaCursoId),
            Fixo<Guid>(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, inscricao.OfertaCursoSegundaOpcaoId),
            Fixo(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno, inscricao.NumeroInscricao),
            submetidaEm,
            Fixo<DateTimeOffset>(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno, inscricao.CanceladaEm),
            Fixo(decisoes, NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal, inscricao.MotivoDoCancelamento),
            ItensDaInscricao.Permitidas(ItensDaInscricao.Secoes(inscricao, fatos, podeRevelar), decisoes),
            [.. ItensDaInscricao.Derivados(inscricao, fatos).Where(decisoes.Permite)],
            [.. arquivos.Where(decisoes.Permite)]));
    }

    /// <summary>Como <c>Fixo</c>, para o valor que se lê do repositório: a leitura só acontece quando a classe é permitida.</summary>
    private static async Task<T?> FixoAsync<T>(
        DecisoesDaProjecao decisoes, NaturezaDoItem natureza, ClassificacaoDeProtecao classe, Func<Task<T?>> ler)
        where T : struct => decisoes.Permite(natureza, classe) ? await ler().ConfigureAwait(false) : null;

    private static async Task<DateTimeOffset?> PrimeiroEnvioAsync(
        Guid inscricaoId, IInscricaoRepository inscricaoRepository, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, DateTimeOffset> envios = await inscricaoRepository
            .ObterPrimeirosEnviosAsync([inscricaoId], cancellationToken).ConfigureAwait(false);

        return envios.TryGetValue(inscricaoId, out DateTimeOffset primeiro) ? primeiro : null;
    }

    private static T? Fixo<T>(DecisoesDaProjecao decisoes, NaturezaDoItem natureza, ClassificacaoDeProtecao classe, T? valor)
        where T : struct => decisoes.Permite(natureza, classe) ? valor : null;

    private static string? Fixo(DecisoesDaProjecao decisoes, NaturezaDoItem natureza, ClassificacaoDeProtecao classe, string? valor) =>
        decisoes.Permite(natureza, classe) ? valor : null;

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

    private static DomainError InscricaoNaoEncontrada { get; } = new(
        InscricaoErrorCodes.NaoEncontrada,
        "Inscrição não encontrada neste processo seletivo.");
}
