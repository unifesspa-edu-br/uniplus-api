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
            .ObterAtivaPorProcessoECpfAsync(command.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
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

        Result adotada = await Services.VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(adotada.Error!);
        }

        Result<Services.ContextoDaVersaoFixada> contexto = await Services.ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, command.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs,
            command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId, cancellationToken)
            .ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(contexto.Error!);
        }

        Dictionary<string, JsonElement> respostasDoCorpo = Services.InscricaoRequestMapper.ParaRespostas(command.Request.Respostas);
        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> gruposLidos = Services.InscricaoRequestMapper.ParaGrupos(command.Request.Grupos);
        if (gruposLidos.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(gruposLidos.Errors);
        }

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = gruposLidos.Value!;
        Result<List<AceiteDeTermo>> aceitesLidos = Services.InscricaoRequestMapper.ParaAceites(command.Request.Aceites);
        if (aceitesLidos.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(aceitesLidos.Errors);
        }

        List<AceiteDeTermo> aceites = aceitesLidos.Value!;

        // O veredito avalia o estado que SERÁ persistido — um campo respondido numa chamada
        // anterior de Salvar e não reenviado aqui continua preenchido, nunca indeterminado; o mesmo
        // vale para o grupo repetível omitido do corpo.
        IReadOnlyDictionary<string, JsonElement> respostasMescladas = inscricao.RespostasAposMesclar(respostasDoCorpo);
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> gruposMesclados = inscricao.GruposAposMesclar(grupos);

        // O endereço sem CEP (ou com complemento longo) é recusado antes de qualquer consulta ao Geo: a
        // recusa que orienta é a que sai primeiro, e não uma divergência de município ou um 503.
        IReadOnlyList<FieldError> doEnderecoSemGeo = [.. Inscricao.RecusasDoEndereco(respostasMescladas, contexto.Value!.Admitidas)];
        if (doEnderecoSemGeo.Count > 0)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(doEnderecoSemGeo);
        }

        // O CEP existir no Geo, ser da cidade declarada e, sendo o geral da cidade, vir com logradouro e
        // bairro: o Geo fora do ar recusa o envio como indisponível, nunca como "CEP não encontrado".
        Result<IReadOnlyList<FieldError>> doEndereco = await Services.ConferenciaDoEnderecoDaInscricao
            .ConferirAsync(respostasMescladas, contexto.Value!.Admitidas, conferenciaEnderecoGeo, aoEnviar: true, cancellationToken)
            .ConfigureAwait(false);
        if (doEndereco.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(doEndereco.Error!);
        }

        if (doEndereco.Value!.Count > 0)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(doEndereco.Value);
        }

        Result<IReadOnlyDictionary<string, JsonElement>> derivados = await Services.DerivadosDoSistemaInscricao
            .CalcularAsync(respostasMescladas, contexto.Value!.DataReferenciaFatos, conferenciaEnderecoGeo, cancellationToken)
            .ConfigureAwait(false);
        if (derivados.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(derivados.Error!);
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(fuso.Error!);
        }

        // A avaliação da definição inteira da versão fixada (ADR-0143), a mesma do rascunho e da
        // consulta do candidato, com as etapas da inscrição concluídas: o envio é o candidato dizendo
        // que terminou. O veredito olha só o que o formulário de inscrição pergunta.
        EnvioDasRespostas envio = new(instante, fuso.Value!);
        ConfiguracaoAvaliavel configuracao = contexto.Value!.Configuracao;
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = Services.AvaliacaoDaInscricaoPorOpcao.Avaliar(contexto.Value!, new EntradaDaAvaliacaoDaInscricao(
            respostasMescladas,
            Services.InscricaoRequestMapper.ParaAvaliacao(gruposMesclados),
            derivados.Value!.ToDictionary(static d => d.Key, static d => FatoResolvido.Resolvido(d.Value), StringComparer.Ordinal),
            AvaliacaoDaInscricao.CodigosDasEtapasDaInscricao(configuracao.Definicao),
            envio,
            NaFaseDaInscricao: true));
        if (avaliacao.IsFailure)
        {
            return Result<ComprovanteInscricaoDto>.Failure(avaliacao.Error!);
        }

        Dictionary<string, JsonElement> respostasFinais = new(respostasMescladas, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in derivados.Value!)
        {
            respostasFinais[codigo] = valor;
        }

        // O nome social que não confere com a conta substitui a obrigatoriedade genérica do campo: a recusa
        // que orienta é a que resolve, e a genérica não diz ao candidato onde cadastrar.
        RespostasAdmitidas admitidas = contexto.Value!.Admitidas with { NomeCivilDoLogin = userContext.NomeCompleto, NomeSocialDoLogin = userContext.NomeSocial };
        IReadOnlyList<FieldError> doNomeSocial = [.. Inscricao.RecusasDoNomeSocial(respostasMescladas, admitidas, aoEnviar: true)];

        // O avaliador descarta em silêncio a resposta cujo código não pertence à oferta vigente
        // (RespostaDeCampo.DentroDaOferta) — sem esta checagem sobre as respostas BRUTAS, uma opção
        // fora da oferta num campo opcional passaria sem recusa.
        IReadOnlyList<FieldError> recusas =
        [
            .. doNomeSocial,
            .. Services.VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostasFinais, gruposMesclados, contexto.Value!.Ofertas),
            .. Services.VeredictoDeSubmissao.Avaliar(
                AvaliacaoDaInscricao.DaInscricao(avaliacao.Value!.Formulario), [.. contexto.Value!.TermosExigidos], aceites, envio.Dia)
                .Where(e => doNomeSocial.Count == 0 || !string.Equals(e.Field, ConjuntoBasicoDaInscricao.NomeSocial, StringComparison.Ordinal)),
        ];
        if (recusas.Count > 0)
        {
            return Result<ComprovanteInscricaoDto>.ValidationFailure(recusas);
        }

        string numeroGerado = await geradorDeNumeroInscricao.ProximoAsync(cancellationToken).ConfigureAwait(false);

        Result<ComprovanteInscricao> submetido = inscricao.Submeter(
            numeroGerado, command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId, respostasDoCorpo, grupos, aceites, admitidas,
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
