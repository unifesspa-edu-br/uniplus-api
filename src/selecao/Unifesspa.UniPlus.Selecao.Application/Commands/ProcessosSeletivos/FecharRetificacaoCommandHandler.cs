namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Fecha a sessão editorial (Story #862, ADR-0110): congela a versão N+1 <b>com a
/// configuração editada</b>, registra o ato por mensagem durável e encerra a sessão — tudo
/// na mesma transação.
/// </summary>
/// <remarks>
/// <para>
/// <b>É aqui que a Feature entrega o que ela existe para entregar.</b> Abrir e descartar
/// deixam o certame como estava; só o fechamento faz a configuração alterada virar
/// documento.
/// </para>
/// <para>
/// As conferências são <b>as mesmas</b> do atalho atômico, e pela mesma razão: o ato é
/// registrado depois, por mensagem durável (ADR-0108), então o que o catálogo de Publicações
/// recusaria tem de ser recusado <b>aqui</b>, com 422, antes de qualquer escrita — senão a
/// versão sai congelada, o cliente recebe 204, e a recusa vira dead letter.
/// </para>
/// <para>
/// <b>Uma recusa não destrói a sessão.</b> Se o congelamento for negado — conformidade
/// insuficiente, ato já retificado, documento não confirmado —, o rascunho <b>permanece
/// aberto</b>, com a configuração editada intacta: o administrador corrige e tenta de novo.
/// </para>
/// </remarks>
public static class FecharRetificacaoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        FecharRetificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoEditalRepository documentoEditalRepository,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoEditalRepository);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return (Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado.")), []);
        }

        // Incondicional nesta rota (D9, "3 antes de 10") — ela existe para a sessão.
        if (!command.Precondicao.Presente)
        {
            return (Result.Failure(new DomainError(
                "Precondicao.Requerida",
                "O fechamento encerra a sessão editorial em curso — informe o If-Match com o ETag dela.")), []);
        }

        if (processo.Rascunho is null)
        {
            return (Result.Failure(new DomainError(
                "RascunhoRetificacao.NaoAberta",
                "Não há retificação em curso neste processo.")), []);
        }

        // A precondição precede as regras de negócio (ADR-0110 D9), e a antecipação é a mesma
        // dos seis Definir*: um cliente com ETag defasado tem de saber disso antes de o
        // servidor ir conferir documento, catálogo de atos e vaga de linhagem — três consultas
        // que ele não errou. O FecharRetificacao do agregado reconfere.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return (Result.Failure(bloqueio), []);
        }

        DocumentoEdital? documento = await documentoEditalRepository
            .ObterPorIdAsync(command.DocumentoEditalId, cancellationToken)
            .ConfigureAwait(false);
        if (documento is null || documento.ProcessoSeletivoId != command.ProcessoSeletivoId)
        {
            return (Result.Failure(new DomainError(
                "ProcessoSeletivo.DocumentoNaoEncontrado",
                $"Documento do Edital {command.DocumentoEditalId} não encontrado ou não pertence a este processo.")), []);
        }

        if (documento.Status != Domain.Enums.StatusDocumentoEdital.Confirmado)
        {
            return (Result.Failure(new DomainError(
                "ProcessoSeletivo.DocumentoNaoConfirmado",
                "Somente um documento confirmado pode ser referenciado no fechamento da retificação.")), []);
        }

        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            command.Numero,
            command.PeriodoInscricaoInicio,
            command.PeriodoInscricaoFim,
            command.DocumentoEditalId);
        if (dadosResult.IsFailure)
        {
            return (Result.Failure(dadosResult.Error!), []);
        }

        DadosEdital dados = dadosResult.Value!;

        Result<TipoAtoPublicadoView> conferenciaDoAto = await ConferenciaDoTipoDeAto
            .CongelaConfiguracaoAsync(tipoDeAtoReader, command.Ato, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoAto.IsFailure)
        {
            return (Result.Failure(conferenciaDoAto.Error!), []);
        }

        TipoAtoPublicadoView tipoConferido = conferenciaDoAto.Value!;

        IReadOnlyList<Guid> atosDaLinhagem = await processoSeletivoRepository
            .ObterAtosCriadoresAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        Result conferenciaDaVaga = await ConferenciaDoTipoDeAto
            .VagaDoObjetoAsync(vagaDeLinhagemReader, tipoConferido, command.ProcessoSeletivoId, atosDaLinhagem, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDaVaga.IsFailure)
        {
            return (Result.Failure(conferenciaDaVaga.Error!), []);
        }

        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versaoAtual is null || versaoAtual.Id != processo.Rascunho.VersaoBaseId)
        {
            return (Result.Failure(new DomainError(
                "RascunhoRetificacao.BaseDesatualizada",
                "A versão sobre a qual esta retificação foi aberta não é mais o topo da cadeia de configuração — "
                + "o fechamento emendaria um ato que já foi emendado.")), []);
        }

        bool jaRetificado = await vagaDeLinhagemReader
            .AtoJaFoiRetificadoAsync(versaoAtual.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false);
        if (jaRetificado)
        {
            return (Result.Failure(new DomainError(
                "ProcessoSeletivo.AtoJaRetificado",
                "O ato que esta retificação emendaria já foi retificado — a cadeia de atos é linear.")), []);
        }

        // O motivo é o do RASCUNHO — declarado na abertura e normalizado uma vez só. É o
        // mesmo valor que o bloco `retificacao` do envelope congela e que viaja para o ato em
        // Publicações; lê-lo de outra fonte faria os dois divergirem.
        string motivo = processo.Rascunho.Motivo;

        // O gate de conformidade precede a canonicalização (ADR-0109 D5) — a retificação
        // também congela uma versão append-only e vinculante.
        if (processo.PendenciaDeConformidade() is { } pendencia)
        {
            return (Result.Failure(pendencia), []);
        }

        Result<ResultadoConformidade> conformidadeLegal = await ConferenciaDeConformidadeLegal
            .AvaliarAsync(obrigatoriedadeLegalRepository, processo, dados.PeriodoInscricaoInicio, cancellationToken)
            .ConfigureAwait(false);
        if (conformidadeLegal.IsFailure)
        {
            return (Result.Failure(conformidadeLegal.Error!), []);
        }

        // Terceira dimensão de conformidade (Story #554, PR #903, ADR-0109 D5): documentos
        // exigidos, coerência da consequência de indeferimento e referência temporal de
        // fatos. A canonicalização abaixo resolve dataReferenciaFatos internamente e LANÇA
        // quando a política não resolve — sem este guard antes dela, um processo inválido
        // vira exceção não tratada em vez do DomainError que o contrato HTTP promete.
        if (processo.PendenciaPreCanonicalizacao() is { } pendenciaPreCanonicalizacao)
        {
            return (Result.Failure(pendenciaPreCanonicalizacao), []);
        }

        // Story #919 (RN08): mesmo congelamento de metadado de fato que a publicação de
        // abertura já faz — a configuração EDITADA pela sessão pode conter gatilho de
        // documento vivo (novo ou alterado), e congelar sem este bloco deixaria o metadado
        // incompleto (vazio) para esta versão.
        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadadosFatosResult =
            await ResolvedorMetadadosFatosCongelados.ResolverAsync(processo, fatoCandidatoReader, cancellationToken)
                .ConfigureAwait(false);
        if (metadadosFatosResult.IsFailure)
        {
            return (Result.Failure(metadadosFatosResult.Error!), []);
        }

        // A configuração canonicalizada é a VIVA — que é, agora, a EDITADA pela sessão. É a
        // diferença inteira em relação ao que a retificação fazia antes desta Feature: ela
        // recanonicalizava a mesma configuração de sempre, e a versão N+1 saía idêntica à N.
        SnapshotCanonico canonico = canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(
                processo,
                dados,
                documento.HashSha256!,
                new RetificacaoInfo(versaoAtual.AtoCriadorId, motivo),
                conformidadeLegal.Value,
                metadadosFatosResult.Value));

        string atorUsuarioSub = userContext.UserId ?? "system";

        Result<VersaoConfiguracao> fechamento = processo.FecharRetificacao(
            dados,
            versaoAtual,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub,
            command.Precondicao,
            timeProvider);
        if (fechamento.IsFailure)
        {
            return (Result.Failure(fechamento.Error!), []);
        }

        VersaoConfiguracao versao = fechamento.Value!;

        await processoSeletivoRepository
            .AdicionarVersaoConfiguracaoAsync(versao, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && VersaoConfiguracaoConstraintViolation.Traduzir(constraint) is { } erroVersao)
        {
            return (Result.Failure(erroVersao), []);
        }

        // Mesma orquestração da abertura e do atalho (ADR-0108): a requisição do ato viaja no
        // outbox, na transação que acabou de gravar a versão nova e apagar o rascunho.
        return (Result.Success(), MensagensDaPublicacao.Montar(
            processo,
            versao,
            command.Ato,
            tipoConferido,
            dados.Numero,
            documento.HashSha256!,
            atoRetificadoId: versaoAtual.AtoCriadorId,
            motivoRetificacao: motivo));
    }
}
