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
        IResolvedorFusoInstitucional resolvedorFuso,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ICalendarioVigenteReader calendarioVigenteReader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoEditalRepository);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(calendarioVigenteReader);
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

        Result<DadosEdital> dadosResult = ResolucaoDoPeriodoDeInscricao.Resolver(
            processo,
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

        // Cascata de remanejamento (RN-CASCATA-1/2/2b/3, Story #575) — cross-dimensão,
        // fora do agregador genérico acima. Mesma antecipação da publicação (ADR-0109 D5).
        if (processo.PendenciaDaCascata() is { } pendenciaCascata)
        {
            return (Result.Failure(pendenciaCascata), []);
        }

        // Resolvido aqui, e não junto da canonicalização, porque a conferência legal abaixo
        // precisa do fuso para derivar o dia civil do início da inscrição (issue #1350). O ponto
        // é entre a cascata e a conferência de propósito: preserva todas as precedências já
        // fixadas e só altera a relação fuso <-> gate legal. Uma falha aqui continua sendo defeito
        // de instalação, mapeado para 500 — o fuso não vira gate de publicação.
        Result<TimeZoneInfo> fusoResult = resolvedorFuso.Resolver();
        if (fusoResult.IsFailure)
        {
            return (Result.Failure(fusoResult.Error!), []);
        }

        TimeZoneInfo fusoInstitucional = fusoResult.Value!;

        Result<ResultadoConformidade> conformidadeLegal = await ConferenciaDeConformidadeLegal
            .AvaliarAsync(obrigatoriedadeLegalRepository, processo, dados.DiaDeReferenciaLegal(fusoInstitucional), modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, cancellationToken)
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

        // Story #1059 (UNI-REQ-0072): uma leitura só do catálogo (D4-bis), compartilhada pelo
        // gate de valor inativo, pela reconferência de coletabilidade abaixo e pelos dois
        // resolvedores que congelam vocabulário de fato — duas leituras abririam janela para o
        // gate aprovar sobre um catálogo e um resolvedor congelar sobre outro, publicando o valor
        // inativo que o gate acabou de recusar.
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, FatoCandidatoView> catalogoPorCodigo =
            catalogoDeFatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

        // Nenhum fato coletado categórico estático pode ter valor inativo no vocabulário
        // declarado, e nenhum predicado do processo pode citar valor inativo — mesmo de um fato
        // que este processo não coleta.
        Result valoresDeDominioAtivos = ConferenciaDeValoresDeDominioAtivos.Conferir(processo, catalogoPorCodigo);
        if (valoresDeDominioAtivos.IsFailure)
        {
            return (Result.Failure(valoresDeDominioAtivos.Error!), []);
        }

        // O catálogo pode reclassificar a Origem de um fato depois que ele já virou
        // FatoColetado (ex.: a migration que reclassificou MODALIDADE de DECLARADO para
        // DERIVADO) — a configuração EDITADA pela sessão pode conter um vínculo (novo ou
        // preexistente) que deixou de ser coletável.
        Result coletabilidadeDosFatos = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogoPorCodigo);
        if (coletabilidadeDosFatos.IsFailure)
        {
            return (Result.Failure(coletabilidadeDosFatos.Error!), []);
        }

        // Story #919 (RN08): mesmo congelamento de metadado de fato que a publicação de
        // abertura já faz — a configuração EDITADA pela sessão pode conter gatilho de
        // documento vivo (novo ou alterado), e congelar sem este bloco deixaria o metadado
        // incompleto (vazio) para esta versão.
        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadadosFatosResult =
            ResolvedorMetadadosFatosCongelados.Resolver(processo, catalogoPorCodigo);
        if (metadadosFatosResult.IsFailure)
        {
            return (Result.Failure(metadadosFatosResult.Error!), []);
        }

        // Story #1059 (UNI-REQ-0072): os valores que o candidato pode escolher para cada fato de
        // seleção coletado — a configuração EDITADA pela sessão pode ter alterado a coleta.
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> valoresSelecionaveisResult =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogoPorCodigo);
        if (valoresSelecionaveisResult.IsFailure)
        {
            return (Result.Failure(valoresSelecionaveisResult.Error!), []);
        }

        // UNI-REQ-0116: UMA leitura do calendário vigente por operação. A mesma resposta
        // alimenta o gate da raiz e o bloco congelado do envelope — ler duas vezes abriria a
        // janela em que o dataset muda entre validar e congelar, e a versão publicada
        // carregaria um calendário que o gate não aprovou.
        CalendarioVigenteView? calendarioVigente = await calendarioVigenteReader
            .ObterVigenteAsync(cancellationToken)
            .ConfigureAwait(false);
        // Dataset vigente incoerente não aborta aqui: quem decide se ele importa é o gate da
        // raiz, e só importa para processo cuja contagem distingue dia útil. Abortar antes
        // recusaria também quem não usa o dado que está quebrado, e o checklist ficaria verde
        // para um processo que a publicação recusa.
        Result<CalendarioDiasUteisCongelado?> calendarioResult =
            LeituraDoCalendarioVigente.Traduzir(calendarioVigente);

        var contexto = new ContextoDeContagemDePrazos(
            calendarioResult.IsSuccess ? calendarioResult.Value : null,
            FusoInstitucionalReconhecido: true,
            FalhaDoCalendarioVigente: calendarioResult.IsFailure ? calendarioResult.Error : null);

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(
                processo,
                dados,
                documento.HashSha256!,
                fusoResult.Value!.Id,
                new RetificacaoInfo(versaoAtual.AtoCriadorId, motivo),
                conformidadeLegal.Value,
                metadadosFatosResult.Value,
                valoresSelecionaveisResult.Value,
                contexto.CalendarioVigente));

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
            timeProvider,
            contexto);
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
