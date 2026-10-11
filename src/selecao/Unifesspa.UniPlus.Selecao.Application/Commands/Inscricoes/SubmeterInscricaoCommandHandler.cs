namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Envia ou corrige a inscrição (issue #1859): a avaliação completa decide o veredito, nunca a
/// gravação — ou tudo passa, ou nada é persistido. Diferente de <see cref="SalvarInscricaoCommandHandler"/>,
/// a avaliação roda sobre o estado FINAL (o que já estava gravado, mesclado com o corpo recebido —
/// nunca só o delta desta chamada), porque a submissão exige obrigatórios do formulário inteiro.
/// </summary>
public static class SubmeterInscricaoCommandHandler
{
    public static async Task<Result<ComprovanteInscricaoDto>> Handle(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IConferenciaEnderecoGeo conferenciaEnderecoGeo,
        IGeradorDeNumeroInscricao geradorDeNumeroInscricao,
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
        ArgumentNullException.ThrowIfNull(geradorDeNumeroInscricao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode submeter a inscrição."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterPorProcessoECpfAsync(command.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<ComprovanteInscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<ComprovanteInscricaoDto>.Failure(erroDono);
        }

        // Um instante só para o envio inteiro: confere o período, é o dia de que se contam os limites
        // da faixa de data e fica no comprovante — quem reavaliar a inscrição pelo comprovante chega ao
        // mesmo veredito, inclusive na virada do dia.
        DateTimeOffset instante = timeProvider.GetUtcNow();
        DomainError? foraDoPeriodo = await Services.PeriodoDaInscricaoResolver.ConferirAsync(
            command.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            instante, cancellationToken).ConfigureAwait(false);
        if (foraDoPeriodo is not null)
        {
            return Result<ComprovanteInscricaoDto>.Failure(foraDoPeriodo);
        }

        Result<Services.ContextoDaVersaoFixada> contexto = await Services.ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, command.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(contexto.Error!);
        }

        Dictionary<string, JsonElement> respostasDoCorpo = Services.InscricaoRequestMapper.ParaRespostas(command.Request.Respostas);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = Services.InscricaoRequestMapper.ParaGrupos(command.Request.Grupos);
        List<AceiteDeTermo> aceites = Services.InscricaoRequestMapper.ParaAceites(command.Request.Aceites);

        // O veredito avalia o estado que SERÁ persistido — um campo respondido numa chamada
        // anterior de Salvar e não reenviado aqui continua preenchido, nunca indeterminado.
        IReadOnlyDictionary<string, JsonElement> respostasMescladas = inscricao.RespostasAposMesclar(respostasDoCorpo);

        Result<IReadOnlyDictionary<string, JsonElement>> derivados = await Services.DerivadosDoSistemaInscricao
            .CalcularAsync(respostasMescladas, contexto.Value!.DataReferenciaFatos, conferenciaEnderecoGeo, cancellationToken)
            .ConfigureAwait(false);
        if (derivados.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(derivados.Error!);
        }

        Dictionary<string, JsonElement> respostasFinais = new(respostasMescladas, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in derivados.Value!)
        {
            respostasFinais[codigo] = valor;
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(fuso.Error!);
        }

        EnvioDasRespostas envio = new(instante, fuso.Value!);
        Result<AvaliacaoFormulario> avaliacao = contexto.Value!.Regras.Avaliar(new EntradaAvaliacaoFormulario(
            respostasFinais,
            EtapasConcluidas: new HashSet<string>(contexto.Value!.Regras.Etapas.Select(static e => e.Codigo), StringComparer.Ordinal),
            FatosConhecidos: new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            envio,
            RespostasDosGrupos: Services.InscricaoRequestMapper.ParaAvaliacao(grupos)));
        if (avaliacao.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(avaliacao.Error!);
        }

        // O avaliador descarta em silêncio a resposta cujo código não pertence à oferta vigente
        // (FormularioPortavel.Avaliar via RespostaDeCampo.DentroDaOferta) — sem esta checagem sobre
        // as respostas BRUTAS, uma opção fora da oferta num campo opcional passaria sem recusa.
        IReadOnlyList<FieldError> recusas =
        [
            .. Services.VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostasFinais, grupos, contexto.Value!.Ofertas),
            .. Services.VeredictoDeSubmissao.Avaliar(avaliacao.Value!, [.. contexto.Value!.TermosExigidos], aceites, envio.Dia),
        ];
        if (recusas.Count > 0)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(recusas);
        }

        string numeroGerado = await geradorDeNumeroInscricao.ProximoAsync(cancellationToken).ConfigureAwait(false);

        Result<ComprovanteInscricao> submetido = inscricao.Submeter(
            numeroGerado, command.Request.OfertaCursoId, respostasDoCorpo, grupos, aceites, contexto.Value!.Admitidas,
            avaliacao.Value!, derivados.Value!, Services.DerivadosDoSistemaInscricao.DefinicoesDasRespostas,
            command.Precondicao, instante);
        if (submetido.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(submetido.Errors);
        }

        await inscricaoRepository.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await inscricaoRepository.AdicionarComprovanteAsync(submetido.Value!, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // O endpoint tem Idempotency-Key (ADR-0119): captura local e descarte, para o
            // SaveChangesAsync do outbox não reencontrar a entidade modificada.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<ComprovanteInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.ConflitoDeConcorrencia, "Esta inscrição foi alterada concorrentemente. Tente novamente."));
        }

        return Result<ComprovanteInscricaoDto>.Success(ComprovanteInscricaoDto.De(submetido.Value!, inscricao.Cpf));
    }
}
