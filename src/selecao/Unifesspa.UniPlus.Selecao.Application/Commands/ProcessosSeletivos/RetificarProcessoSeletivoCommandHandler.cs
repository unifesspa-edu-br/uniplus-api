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
/// Handler convention-based do <see cref="RetificarProcessoSeletivoCommand"/>
/// (RN08, Story #759 T5 #786, ADR-0101 + ADR-0005): carrega o agregado com a
/// cadeia de Editais, valida o documento confirmado (T3), canonicaliza a
/// configuração acrescida do bloco de retificação (ADR-0100/0101) e delega a
/// orquestração de negócio a <see cref="ProcessoSeletivo.Retificar"/>.
/// Cascading messages — o <see cref="Domain.Events.ProcessoPublicadoEvent"/>
/// só é drenado depois do <c>SaveChanges</c> bem-sucedido.
/// </summary>
public static class RetificarProcessoSeletivoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        RetificarProcessoSeletivoCommand command,
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
                "Somente um documento confirmado pode ser referenciado na retificação.")), []);
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

        // O ato é registrado depois, por mensagem durável (ADR-0108). O que o catálogo de
        // Publicações recusaria tem de ser recusado AQUI, com 422, antes de qualquer escrita
        // — senão o Edital sai publicado, o cliente recebe 204, e a recusa vira dead letter.
        Result<TipoAtoPublicadoView> conferenciaDoAto = await ConferenciaDoTipoDeAto
            .CongelaConfiguracaoAsync(tipoDeAtoReader, command.Ato, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoAto.IsFailure)
        {
            return (Result.Failure(conferenciaDoAto.Error!), []);
        }

        // Os atributos CONFERIDOS viajam na mensagem: o catálogo é editável, e reler no
        // consumo faria a decisão tomada aqui (que já devolveu 204) valer outra coisa.
        TipoAtoPublicadoView tipoConferido = conferenciaDoAto.Value!;

        // A vaga que a linhagem reserva sobre o certame (ADR-0107) é monotônica: ocupada,
        // nunca se libera. Se já estiver tomada por outra linhagem, o registro do ato seria
        // recusado no consumo da fila e o certame ficaria publicado sem ato — a recusa tem
        // de vir agora.
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


        // A versão de configuração é agregado próprio (ADR-0104) — não coleção da
        // raiz. O handler carrega a corrente (maior NumeroVersao) e a entrega a
        // Retificar, que sucede a cadeia a partir dela. Sem versão corrente, ou o
        // processo não foi publicado, ou está inconsistente — a mesma transição
        // que Retificar barra, antecipada aqui para não canonicalizar em vão.
        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versaoAtual is null)
        {
            return (Result.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {processo.Status}.")), []);
        }

        // A cadeia de atos é linear: um ato é emendado no máximo uma vez (ADR-0103). O ato que
        // esta retificação vai emendar pode já ter sido emendado por fora — pelo endpoint
        // administrativo de Publicações. Sem conferir agora, o registro recusaria com
        // RaizJaRetificada e a retificação ficaria publicada sem ato.
        bool jaRetificado = await vagaDeLinhagemReader
            .AtoJaFoiRetificadoAsync(versaoAtual.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false);
        if (jaRetificado)
        {
            return (Result.Failure(new DomainError(
                "ProcessoSeletivo.AtoJaRetificado",
                "O ato que esta retificação emendaria já foi retificado — a cadeia de atos é linear.")), []);
        }

        // Normaliza o motivo UMA vez, aqui (Trim + NFC), e usa o mesmo valor
        // nos dois caminhos: o bloco 'retificacao' do snapshot e o
        // MotivoRetificacao do Edital. O canonicalizer aplica NormalizeNfc ao
        // congelar o snapshot; se o Edital guardasse o valor sem a mesma
        // normalização, um input Unicode decomposto (ex.: "correção" em NFD)
        // divergiria entre a coluna motivo_retificacao e o bloco congelado,
        // quebrando a reconciliação. Postgres não normaliza texto, então a
        // paridade tem de ser garantida na aplicação. NormalizeNfc é
        // idempotente — reaplicá-lo no canonicalizer não altera o valor.
        string motivo = HashCanonicalComputer.NormalizeNfc(command.Motivo.Trim());

        // O gate precede a canonicalização, igual à publicação (ADR-0109 D5) — a
        // retificação também congela uma versão append-only e vinculante.
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
        // DERIVADO) — uma retificação também pode congelar um vínculo que deixou de ser
        // coletável desde a publicação de abertura.
        Result coletabilidadeDosFatos = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogoPorCodigo);
        if (coletabilidadeDosFatos.IsFailure)
        {
            return (Result.Failure(coletabilidadeDosFatos.Error!), []);
        }

        // Story #919 (RN08): mesmo congelamento de metadado de fato que a publicação de
        // abertura já faz — uma retificação também pode conter gatilho de documento vivo, e
        // congelar sem este bloco deixaria o metadado incompleto (vazio) para esta versão.
        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadadosFatosResult =
            ResolvedorMetadadosFatosCongelados.Resolver(processo, catalogoPorCodigo);
        if (metadadosFatosResult.IsFailure)
        {
            return (Result.Failure(metadadosFatosResult.Error!), []);
        }

        // Story #1059 (UNI-REQ-0072): os valores que o candidato pode escolher para cada fato de
        // seleção coletado — mesmo raciocínio da publicação de abertura.
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

        Result<VersaoConfiguracao> retificarResult = processo.Retificar(
            dados,
            versaoAtual,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub,
            motivo,
            timeProvider,
            contexto);
        if (retificarResult.IsFailure)
        {
            return (Result.Failure(retificarResult.Error!), []);
        }

        VersaoConfiguracao versao = retificarResult.Value!;

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
            // Traduz violação de guard rail de banco (ADR-0102). Duas retificações
            // concorrentes do mesmo processo elegem o MESMO ato criador como alvo e
            // derivam o mesmo N+1: ux_versoes_configuracao_processo_numero e o trigger de
            // sucessão (ck_versoes_configuracao_cadeia) deixam passar uma só, na mesma
            // transação. É aqui que a linearidade da cadeia é garantida — Publicações
            // também a barra, mas só no consumo da fila, depois do commit.
            // Filtro do `when` garante que outras exceções propagam intactas.
            return (Result.Failure(erroVersao), []);
        }

        // ADR-0108: a retificação segue a MESMA orquestração da abertura — a requisição do
        // ato viaja no outbox, na transação que acabou de gravar a nova versão. O que muda
        // é o par (ato retificado, motivo): em Publicações, retificar é publicar um ato que
        // emenda outro (ADR-0103), e o ato emendado é o que criou a versão anterior — o
        // mesmo alvo que o agregado elegeu, não o de maior data. O tipo do ato continua
        // vindo declarado pelo operador: uma convocação retificada continua convocação.
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
