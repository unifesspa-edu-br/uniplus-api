namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Salva o rascunho (issue #1859) sobre a versão do formulário fixada ao iniciar — nunca a vigente
/// por relógio. A validação é estrutural: só recusa por campo a resposta que viola uma restrição do
/// formulário; nunca por obrigatoriedade, que é exigida somente na submissão.
/// </summary>
public static class SalvarInscricaoCommandHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        SalvarInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IConferenciaEnderecoGeo conferenciaEnderecoGeo,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(conferenciaEnderecoGeo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode salvar a inscrição."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterPorProcessoECpfAsync(command.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<InscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<InscricaoDto>.Failure(erroDono);
        }

        // O mesmo instante confere o período e é o envio de que se contam os limites da faixa de data.
        DateTimeOffset instante = timeProvider.GetUtcNow();
        DomainError? foraDoPeriodo = await Services.PeriodoDaInscricaoResolver.ConferirAsync(
            command.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            instante, cancellationToken).ConfigureAwait(false);
        if (foraDoPeriodo is not null)
        {
            return Result<InscricaoDto>.Failure(foraDoPeriodo);
        }

        // A inscrição enviada não aceita rascunho: recusar antes das respostas evita pedir correção
        // de um campo numa gravação que não seria aceita de jeito nenhum. Vem depois do período,
        // porque fora dele nem o novo envio que a recusa orienta seria aceito.
        if (inscricao.ConferirAindaEmRascunho() is { } erroRascunho)
        {
            return Result<InscricaoDto>.Failure(erroRascunho);
        }

        Result<Services.ContextoDaVersaoFixada> contexto = await Services.ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, command.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs,
            command.Request.OfertaCursoId, cancellationToken)
            .ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<InscricaoDto>.Failure(contexto.Error!);
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<InscricaoDto>.Failure(fuso.Error!);
        }

        EnvioDasRespostas envio = new(instante, fuso.Value!);
        Dictionary<string, JsonElement> respostas = Services.InscricaoRequestMapper.ParaRespostas(command.Request.Respostas);
        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> gruposLidos = Services.InscricaoRequestMapper.ParaGrupos(command.Request.Grupos);
        if (gruposLidos.IsFailure)
        {
            return Result<InscricaoDto>.ValidationFailure(gruposLidos.Errors);
        }

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = gruposLidos.Value!;

        Result<List<AceiteDeTermo>> aceitesLidos = Services.InscricaoRequestMapper.ParaAceites(command.Request.Aceites);
        if (aceitesLidos.IsFailure)
        {
            return Result<InscricaoDto>.ValidationFailure(aceitesLidos.Errors);
        }

        List<AceiteDeTermo> aceites = aceitesLidos.Value!;

        // A mesma avaliação do envio e da consulta (ADR-0143), sem etapa concluída: o rascunho ainda
        // está sendo preenchido, e só o que o corpo traz é conferido.
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = AvaliacaoDaInscricao.Avaliar(contexto.Value!.Configuracao, new EntradaDaAvaliacaoDaInscricao(
            respostas,
            Services.InscricaoRequestMapper.ParaAvaliacao(grupos),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            envio,
            NaFaseDaInscricao: true));
        if (avaliacao.IsFailure)
        {
            return Result<InscricaoDto>.Failure(avaliacao.Error!);
        }

        IReadOnlyList<FieldError> recusas = RecusasEstruturais(AvaliacaoDaInscricao.DaInscricao(avaliacao.Value!.Formulario), envio.Dia);
        if (recusas.Count > 0)
        {
            return Result<InscricaoDto>.ValidationFailure(recusas);
        }

        // O CEP existir no Geo e ser da cidade declarada vale já no rascunho; o Geo fora do ar adia a
        // conferência para o envio, e o rascunho continua salvável.
        Result<IReadOnlyList<FieldError>> doEndereco = await Services.ConferenciaDoEnderecoDaInscricao
            .ConferirAsync(respostas, contexto.Value!.Admitidas, conferenciaEnderecoGeo, aoEnviar: false, cancellationToken)
            .ConfigureAwait(false);
        if (doEndereco.IsFailure)
        {
            return Result<InscricaoDto>.Failure(doEndereco.Error!);
        }

        if (doEndereco.Value!.Count > 0)
        {
            return Result<InscricaoDto>.ValidationFailure(doEndereco.Value);
        }

        Result salvar = inscricao.Salvar(
            command.Request.OfertaCursoId, respostas, grupos, aceites, contexto.Value!.Admitidas with { NomeCivilDoLogin = userContext.NomeCompleto, NomeSocialDoLogin = userContext.NomeSocial }, command.Precondicao);
        if (salvar.IsFailure)
        {
            return Result<InscricaoDto>.ValidationFailure(salvar.Errors);
        }

        await inscricaoRepository.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // A avaliação devolvida é a do que ficou gravado, e não se grava: o rascunho a recalcula.
        Result<ResultadoDaAvaliacaoDaInscricao> avaliada = await Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            inscricao,
            contexto.Value!,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            envio,
            conferenciaEnderecoGeo,
            cancellationToken).ConfigureAwait(false);
        if (avaliada.IsFailure)
        {
            return Result<InscricaoDto>.Failure(avaliada.Error!);
        }

        Result<AvaliacaoDaInscricaoDto> projetada = Services.ProjecaoDaAvaliacaoDaInscricao.Projetar(
            avaliada.Value!, contexto.Value!, inscricao.ProcessoSeletivoId, enderecoNoAcervo);
        return projetada.IsFailure
            ? Result<InscricaoDto>.Failure(projetada.Error!)
            : Result<InscricaoDto>.Success(InscricaoDto.De(inscricao, projetada.Value!));
    }

    /// <summary>
    /// Só a resposta que viola uma restrição do formulário é recusada — a ausência de resposta a um
    /// item obrigatório não é, porque o rascunho não exige obrigatórios. Cobre os itens de nível
    /// superior e os de cada ocorrência de grupo repetível: o membro do grupo responde um item como
    /// qualquer outro, e uma restrição violada ali é tão inválida quanto no nível superior.
    /// </summary>
    private static IReadOnlyList<FieldError> RecusasEstruturais(AvaliacaoFormulario avaliacao, DateOnly diaDoEnvio) =>
    [
        .. avaliacao.Itens
            .Where(static item => item.RestricoesVioladas.Count > 0)
            .Select(item => Services.RecusaDaResposta.De(item.FatoCodigo, item, diaDoEnvio)),
        .. avaliacao.Grupos
            .SelectMany(grupo => grupo.Ocorrencias.SelectMany(ocorrencia => ocorrencia.Itens
                .Where(static item => item.RestricoesVioladas.Count > 0)
                .Select(item => Services.RecusaDaResposta.De($"{grupo.Codigo}.{ocorrencia.Id}.{item.FatoCodigo}", item, diaDoEnvio)))),
    ];
}
