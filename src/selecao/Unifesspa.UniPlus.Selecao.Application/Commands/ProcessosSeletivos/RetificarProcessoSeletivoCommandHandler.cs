namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Application.Abstractions.Authentication;
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
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
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

        // O ato retificado é o que criou a versão corrente — o topo da cadeia de
        // CONFIGURAÇÃO (ADR-0104), não o ato de maior data documental. É o mesmo
        // alvo que ProcessoSeletivo.Retificar elege; congelar aqui um id diferente
        // faria o bloco 'retificacao' do envelope apontar para outro documento.
        SnapshotCanonico canonico = canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(
                processo,
                dados,
                documento.HashSha256!,
                new RetificacaoInfo(versaoAtual.AtoCriadorId, motivo)));

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
            timeProvider);
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
