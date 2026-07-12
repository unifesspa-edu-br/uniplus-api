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
            .ObterComConfiguracaoAsync(command.ProcessoSeletivoId, cancellationToken)
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

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(processo, dados, documento.HashSha256!);

        string atorUsuarioSub = userContext.UserId ?? "system";

        Result<PublicacaoResultado> publicarResult = processo.Publicar(
            dados,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub,
            // A data que o DOCUMENTO declara (ADR-0108) — a mesma que Publicações registra no
            // ato. Sem isto, o mesmo documento teria uma data aqui e outra lá. Quem ordena as
            // versões continua sendo o relógio do sistema (ADR-0104); esta data não ordena nada.
            DataDocumental(command.Ato),
            timeProvider);
        if (publicarResult.IsFailure)
        {
            return (Result.Failure(publicarResult.Error!), []);
        }

        await processoSeletivoRepository
            .AdicionarVersaoConfiguracaoAsync(publicarResult.Value!.Versao, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint)
        {
            // Traduz violação de guard rail de banco (ADR-0102) — corrida de
            // duas publicações concorrentes do mesmo processo, ou gap de
            // validação em memória. Filtro do `when` garante que outras
            // exceções não mapeadas propagam intactas.
            if (UniqueConstraintViolation.IsAberturaJaExiste(constraint))
            {
                return (Result.Failure(new DomainError(
                    "Edital.AberturaJaExiste",
                    "Este processo já tem um Edital de abertura publicado.")), []);
            }

            if (UniqueConstraintViolation.IsContratoNaturezaInvalido(constraint))
            {
                return (Result.Failure(new DomainError(
                    "Edital.ContratoNaturezaInvalido",
                    "Abertura não carrega edital retificado nem motivo; retificação exige ambos.")), []);
            }

            if (VersaoConfiguracaoConstraintViolation.Traduzir(constraint) is { } erroVersao)
            {
                return (Result.Failure(erroVersao), []);
            }

            throw;
        }

        // ADR-0108: a requisição de registro do ato viaja como cascading message, junto dos
        // domain events — o Wolverine instala o envelope no outbox DENTRO da transação que
        // acabou de gravar o Edital e a versão (ADR-0004). Ou os dois existem, ou nenhum.
        //
        // É essa atomicidade que a chamada síncrona não conseguia dar, e é o que impede o
        // ato órfão: a vaga de linhagem do certame (ADR-0107) é monotônica, e um ato
        // registrado para uma publicação que falhou depois deixaria o certame impublicável
        // para sempre.
        //
        // O ato ainda não existe quando esta linha roda — e não precisa existir: o id é
        // decidido AQUI, e a versão já o referencia por VALOR, sem chave estrangeira
        // (ADR-0061). O modelo sempre previu que o ato viveria noutro módulo.
        return (Result.Success(), MensagensDaPublicacao.Montar(
            processo,
            publicarResult.Value!,
            command.Ato,
            tipoConferido,
            dados.Numero,
            documento.HashSha256!,
            atoRetificadoId: null,
            motivoRetificacao: null));
    }

    /// <summary>
    /// A data documental declarada, no instante convencional de início do dia em UTC. O ato
    /// a declara como data (<c>DateOnly</c>); o Edital a guarda como instante — a conversão é
    /// convencional e não carrega hora, porque o documento declara um DIA.
    /// </summary>
    private static DateTimeOffset DataDocumental(DadosDoAto ato) =>
        new(ato.DataPublicacao.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
