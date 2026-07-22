namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Abre a sessão editorial de retificação (Story #860, ADR-0110 D3).
/// </summary>
/// <remarks>
/// <b>Não congela nada.</b> Nenhuma <see cref="VersaoConfiguracao"/> é aberta, nenhum ato é
/// requisitado, nenhum evento é drenado — o handler devolve <c>Result</c>, e não a tupla
/// com cascading messages que a publicação e a retificação devolvem. A versão nova nasce
/// só no fechamento (S5); até lá, o que vale para o mundo continua sendo a versão
/// congelada vigente.
/// </remarks>
public static class AbrirRetificacaoCommandHandler
{
    public static async Task<Result<RetificacaoEmCursoDto>> Handle(
        AbrirRetificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<RetificacaoEmCursoDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A versão corrente é capturada sob o MESMO FOR UPDATE que carregou a raiz: uma
        // publicação ou retificação concorrente que sucedesse a cadeia entre a leitura da
        // versão e a gravação do rascunho o deixaria ancorado numa base que já não é o
        // topo — e o fechamento emendaria um ato já emendado.
        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versaoAtual is null)
        {
            return Result<RetificacaoEmCursoDto>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {processo.Status}."));
        }

        // A base tem de ser REIDRATÁVEL, e a recusa vem AGORA — na abertura, não no
        // descarte. Uma sessão aberta sobre uma versão que o sistema não sabe reconstruir
        // (a 1.0, que pode congelar `nao_construido` nos blocos que a ADR-0109 D8 tornou
        // obrigatórios) é uma sessão IMPOSSÍVEL DE DESCARTAR: o administrador só
        // descobriria ao tentar desistir, e a única saída seria fechar uma retificação que
        // ele não queria. Recusar na porta é o que impede o beco sem saída.
        if (PendenciaDeReidratacao(registroCodecs, versaoAtual.SchemaVersion) is { } pendencia)
        {
            return Result<RetificacaoEmCursoDto>.Failure(pendencia);
        }

        string abertoPorSub = userContext.UserId ?? "system";

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            command.Motivo,
            versaoAtual,
            abertoPorSub,
            timeProvider.GetUtcNow());
        if (abertura.IsFailure)
        {
            return Result<RetificacaoEmCursoDto>.Failure(abertura.Error!);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && RascunhoRetificacaoConstraintViolation.Traduzir(constraint) is { } erro)
        {
            // Duas aberturas concorrentes leem o agregado sem rascunho e passam ambas pela
            // checagem em memória. É o índice único que decide — a garantia é do banco, não
            // da disciplina do caller (ADR-0102). O filtro do `when` deixa qualquer outra
            // exceção propagar intacta.
            return Result<RetificacaoEmCursoDto>.Failure(erro);
        }

        return Result<RetificacaoEmCursoDto>.Success(RetificacaoEmCursoDto.De(abertura.Value!));
    }

    /// <summary>
    /// A versão do envelope da base é conhecida <b>e</b> reidratável? A recusa é
    /// <b>nomeada</b>: quem abre precisa distinguir uma versão que o sistema não conhece de
    /// uma que ele conhece e não sabe reconstruir.
    /// </summary>
    private static DomainError? PendenciaDeReidratacao(IRegistroCodecsEnvelope registroCodecs, string schemaVersion)
    {
        CapacidadeCodec? capacidade = registroCodecs.Capacidades
            .FirstOrDefault(c => string.Equals(c.SchemaVersion, schemaVersion, StringComparison.Ordinal));

        if (capacidade is null)
        {
            return new DomainError(
                "EnvelopeCodec.VersaoDesconhecida",
                $"A versão {schemaVersion} do envelope congelado não está no registro de codecs — não há como reconstruir esta configuração, e a retificação não poderia ser descartada.");
        }

        if (!capacidade.Reidratavel)
        {
            return new DomainError(
                "EnvelopeCodec.VersaoNaoReidratavel",
                $"A versão {schemaVersion} do envelope é conhecida, mas não pode ser reidratada ({capacidade.MotivoDaRecusa}) — abrir a retificação criaria uma sessão impossível de descartar.");
        }

        return null;
    }
}
