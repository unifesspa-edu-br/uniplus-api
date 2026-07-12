namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Application.Abstractions.Authentication;

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
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoEditalRepository);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
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

        // O ato retificado é o que criou a versão corrente — o topo da cadeia de
        // CONFIGURAÇÃO (ADR-0104), não o Edital de maior data documental. É o mesmo
        // alvo que ProcessoSeletivo.Retificar elege; congelar aqui um id diferente
        // faria o bloco 'retificacao' do snapshot apontar para outro documento.
        SnapshotCanonico canonico = canonicalizer.Canonicalizar(
            processo,
            dados,
            documento.HashSha256!,
            new RetificacaoInfo(versaoAtual.AtoCriadorId, motivo));

        string atorUsuarioSub = userContext.UserId ?? "system";

        Result<PublicacaoResultado> retificarResult = processo.Retificar(
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

        await processoSeletivoRepository
            .AdicionarVersaoConfiguracaoAsync(retificarResult.Value!.Versao, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint)
        {
            // Traduz violação de guard rail de banco (ADR-0102) — corrida de
            // duas retificações concorrentes do mesmo processo, ou gap de
            // validação em memória. Filtro do `when` garante que outras
            // exceções não mapeadas propagam intactas.
            if (UniqueConstraintViolation.IsDataPublicacaoDuplicada(constraint))
            {
                return (Result.Failure(new DomainError(
                    "Edital.DataPublicacaoDuplicada",
                    "Já existe um Edital publicado neste processo com a mesma data de publicação.")), []);
            }

            if (UniqueConstraintViolation.IsContratoNaturezaInvalido(constraint))
            {
                return (Result.Failure(new DomainError(
                    "Edital.ContratoNaturezaInvalido",
                    "Abertura não carrega edital retificado nem motivo; retificação exige ambos.")), []);
            }

            if (UniqueConstraintViolation.IsRetificacaoDuplicada(constraint))
            {
                return (Result.Failure(new DomainError(
                    "Edital.RetificacaoJaExiste",
                    "Este Edital já foi retificado — a cadeia de retificação é linear.")), []);
            }

            if (VersaoConfiguracaoConstraintViolation.Traduzir(constraint) is { } erroVersao)
            {
                return (Result.Failure(erroVersao), []);
            }

            throw;
        }

        // ADR-0005 + ADR-0041: drenagem por cascading messages, DEPOIS do
        // SaveChanges bem-sucedido — nunca antes (janela de perda em rollback).
        return (Result.Success(), processo.DequeueDomainEvents().Cast<object>());
    }
}
