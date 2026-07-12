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

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(processo, dados, documento.HashSha256!);

        string atorUsuarioSub = userContext.UserId ?? "system";

        Result<PublicacaoResultado> publicarResult = processo.Publicar(
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

        // SPIKE #820: a requisição de registro do ato viaja como cascading message, junto
        // dos domain events — o Wolverine instala o envelope no outbox DENTRO da transação
        // que acabou de gravar o Edital e a versão (ADR-0004). Ou os dois existem, ou
        // nenhum: é a atomicidade que a chamada síncrona não conseguia dar, e é o que
        // impede o ato órfão que reservaria a vaga do certame para sempre (ADR-0107).
        //
        // O ato ainda não existe quando esta linha roda — e não precisa existir: o id é
        // decidido aqui e a versão já o referencia por VALOR, sem chave estrangeira
        // (ADR-0061). O modelo sempre previu que o ato viveria noutro módulo.
        object[] mensagens =
        [
            .. processo.DequeueDomainEvents().Cast<object>(),
            new RegistrarAtoNormativoRequisicao(
                AtoId: publicarResult.Value!.Versao.AtoCriadorId,
                Orgao: DadosDoAtoSpike.Orgao,
                Serie: DadosDoAtoSpike.Serie,
                Ano: DadosDoAtoSpike.Ano,
                Numero: dados.Numero,
                TipoCodigo: DadosDoAtoSpike.TipoAbertura,
                DataPublicacao: DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
                DocumentoHash: documento.HashSha256!,
                Assinante: DadosDoAtoSpike.Assinante,
                VersaoInvocadaId: publicarResult.Value!.Versao.Id,
                VersaoInvocadaHash: publicarResult.Value!.Versao.HashConfiguracao,
                AtoRetificadoId: null,
                MotivoRetificacao: null,
                Vinculos: [new VinculoEntidadeRequisicao(DadosDoAtoSpike.EntidadeProcessoSeletivo, processo.Id)]),
        ];

        return (Result.Success(), mensagens);
    }
}
