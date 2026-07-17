namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler convention-based do <see cref="PublicarProcessoSeletivoCommand"/>
/// (RN08, Story #759 T4 #785, ADR-0005 + ADR-0041): carrega o agregado,
/// valida o documento confirmado (T3), canonicaliza a configuração
/// (ADR-0100) e delega a orquestração de negócio a
/// <see cref="ProcessoSeletivo.Publicar"/>. Cascading messages — o
/// <see cref="Domain.Events.ProcessoPublicadoEvent"/> é drenado só depois do
/// <c>SaveChanges</c> bem-sucedido.
/// </summary>
public static class PublicarProcessoSeletivoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoEditalRepository documentoEditalRepository,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
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
                "Somente um documento confirmado pode ser referenciado na publicação.")), []);
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

        // O gate precede a canonicalização (ADR-0109 D5): um processo não conforme
        // não chega a ser projetado. Sem isso, a canonicalização de uma dimensão
        // obrigatória ausente falharia alto (D8) em vez de devolver o DomainError
        // que o contrato HTTP promete. A raiz reavalia — este é o guarda antecipado,
        // não a autoridade.
        if (processo.PendenciaDeConformidade() is { } pendencia)
        {
            return (Result.Failure(pendencia), []);
        }

        // Segunda dimensão de conformidade, ao lado da estrutural — mesma antecipação,
        // mesmo motivo (ADR-0109 D5): um processo não conforme não chega a ser projetado.
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

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, documento.HashSha256!, Conformidade: conformidadeLegal.Value));

        string atorUsuarioSub = userContext.UserId ?? "system";

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dados,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub,
            timeProvider);
        if (publicarResult.IsFailure)
        {
            return (Result.Failure(publicarResult.Error!), []);
        }

        VersaoConfiguracao versao = publicarResult.Value!;

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
            // Traduz violação de guard rail de banco (ADR-0102): a corrida de duas
            // publicações concorrentes do mesmo processo cai em
            // ux_versoes_configuracao_processo_numero — as duas derivam a versão 1, e o
            // índice deixa passar uma só. É o backstop transacional que substitui o
            // antigo índice de abertura única, e sem literal de tipo de ato no filtro.
            // Filtro do `when` garante que outras exceções propagam intactas.
            return (Result.Failure(erroVersao), []);
        }

        // ADR-0108: a requisição de registro do ato viaja como cascading message, junto dos
        // domain events — o Wolverine instala o envelope no outbox DENTRO da transação que
        // acabou de gravar a versão (ADR-0004). Ou os dois existem, ou nenhum.
        //
        // É essa atomicidade que a chamada síncrona não conseguia dar, e é o que impede o
        // ato órfão: a vaga de linhagem do certame (ADR-0107) é monotônica, e um ato
        // registrado para uma publicação que falhou depois deixaria o certame impublicável
        // para sempre.
        //
        // O ato ainda não existe quando esta linha roda — e não precisa existir: o id é
        // decidido pelo agregado, e a versão já o referencia por VALOR, sem chave
        // estrangeira (ADR-0061). O documento normativo é de Publicações (ADR-0103/0105);
        // Seleção guarda dele apenas o par {id, hash}.
        return (Result.Success(), MensagensDaPublicacao.Montar(
            processo,
            versao,
            command.Ato,
            tipoConferido,
            dados.Numero,
            documento.HashSha256!,
            atoRetificadoId: null,
            motivoRetificacao: null));
    }
}
