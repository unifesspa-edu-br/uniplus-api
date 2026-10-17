namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Application.Abstractions;

using Domain.Entities;
using Domain.Events;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

// Handler do cenário de rollback (V9): semeia um processo conforme, publica
// de verdade (mesma orquestração de ProcessoSeletivo.Publicar do handler
// produtivo) e força uma exceção logo após o SaveChanges — nunca chega a
// retornar o IEnumerable<object> de cascading. O codegen do Wolverine
// reconhece IEnumerable<object> no retorno normal e instala
// CaptureCascadingMessages no postprocessor; como a chain está dentro de
// EnrollDbContextInTransaction, o throw pós-SaveChanges reverte também os
// INSERTs já executados na mesma transação ambiente (ADR-0005).
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class FalharAposPublicarCascadingHandler
{
    public const string MensagemErro = "Cenário V9 — exceção forçada após SaveChanges no caminho cascading";

    public static async Task<IEnumerable<object>> Handle(
        FalharAposPublicarCascadingCommand command,
        SelecaoDbContext db,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, command.NomeProcesso)
            .ConfigureAwait(false);

        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: null,
            periodoInscricaoInicio: DateTimeOffset.UtcNow,
            periodoInscricaoFim: DateTimeOffset.UtcNow.AddDays(30),
            documentoEditalId: documento.Id);

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosResult.Value!, documento.HashSha256!, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados()));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dadosResult.Value!,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "cascading-v9-test",
            timeProvider, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        db.VersoesConfiguracao.Add(publicarResult.Value!);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        throw new InvalidOperationException(MensagemErro);
    }
}

// Subscritor in-memory de ProcessoPublicadoEvent — drena pela queue PG
// "domain-events" registrada na configuração produtiva e grava no coletor.
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class ProcessoPublicadoSubscriberHandler
{
    public static void Handle(
        ProcessoPublicadoEvent @event,
        DomainEventCollector collector)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(collector);

        collector.Record(@event);
    }
}

// Registra o ato do processo e envia a requisição de registro na mesma transação — o arranjo que o
// comando de publicação do ato avulso usa. O processamento é o do handler produtivo de
// AtoNormativoRegistrado, que devolve o ato registrado a este registro.
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Handlers convencionais do Wolverine devem ser públicos.")]
public sealed class RegistrarAtoDoProcessoDeTesteHandler
{
    public const string MensagemErro = "Falha forçada depois de gravar o registro do ato do processo";

    public static async Task<IEnumerable<object>> Handle(
        RegistrarAtoDoProcessoDeTesteCommand command,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateOnly data = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                command.AtoId, command.ProcessoSeletivoId, DadosDoAtoDeTeste.TipoAviso, "Aviso", "3/2026", data,
                "Diretor do CEPS", "ceps", "psm-medicina-2027", command.DocumentoPrincipalId, []),
            timeProvider);

        await atoDoProcessoRepository.AdicionarAsync(ato, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        if (command.FalharDepoisDeGravar)
        {
            throw new InvalidOperationException(MensagemErro);
        }

        return
        [
            new RegistrarAtoNormativoRequisicao(
                AtoId: ato.Id,
                Orgao: "CEPS",
                Serie: "AVISO",
                Ano: 2026,
                Numero: ato.Numero,
                TipoCodigo: ato.TipoCodigo,
                DataPublicacao: data,
                DocumentoHash: new string('a', 64),
                Assinante: ato.Assinante,
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: null,
                MotivoRetificacao: null,
                Vinculos: [new VinculoEntidadeRequisicao("PROCESSO_SELETIVO", ato.ProcessoSeletivoId)],
                AtributosDoTipo: new AtributosDoTipoAto(false, false, false, ato.TipoNome)),
        ];
    }
}
