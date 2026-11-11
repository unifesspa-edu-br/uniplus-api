namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

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
    public static async Task<Result<ReciboDaInscricaoDto>> Handle(
        SubmeterInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
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
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(conferenciaEnderecoGeo);
        ArgumentNullException.ThrowIfNull(geradorDeNumeroInscricao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<ReciboDaInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode submeter a inscrição."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(command.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<ReciboDaInscricaoDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<ReciboDaInscricaoDto>.Failure(erroDono);
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
            return Result<ReciboDaInscricaoDto>.Failure(foraDoPeriodo);
        }

        Result adotada = await Services.VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.Failure(adotada.Error!);
        }

        Result<Services.ContextoDaVersaoFixada> contexto = await Services.ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, command.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs,
            command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId, cancellationToken)
            .ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.Failure(contexto.Error!);
        }

        Dictionary<string, JsonElement> respostasDoCorpo = Services.InscricaoRequestMapper.ParaRespostas(command.Request.Respostas);
        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> gruposLidos = Services.InscricaoRequestMapper.ParaGrupos(command.Request.Grupos);
        if (gruposLidos.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.ValidationFailure(gruposLidos.Errors);
        }

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = gruposLidos.Value!;
        Result<List<AceiteDeTermo>> aceitesLidos = Services.InscricaoRequestMapper.ParaAceites(command.Request.Aceites);
        if (aceitesLidos.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.ValidationFailure(aceitesLidos.Errors);
        }

        List<AceiteDeTermo> aceites = aceitesLidos.Value!;

        // O veredito avalia o estado que SERÁ persistido — um campo respondido numa chamada
        // anterior de Salvar e não reenviado aqui continua preenchido, nunca indeterminado; o mesmo
        // vale para o grupo repetível omitido do corpo.
        IReadOnlyDictionary<string, JsonElement> respostasMescladas = inscricao.RespostasAposMesclar(respostasDoCorpo);
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> gruposMesclados = inscricao.GruposAposMesclar(grupos);

        RespostasAdmitidas admitidasDaVersao = contexto.Value!.Admitidas;
        ConfiguracaoAvaliavel configuracao = contexto.Value!.Configuracao;

        // As recusas se acumulam e a resposta é uma só, no fim. 1) As que não dependem do Geo: a forma do corpo e o endereço
        // sem CEP ou com complemento longo. O campo recusado não é conferido no Geo nem entra nos derivados.
        List<FieldError> recusas =
        [
            .. Inscricao.RecusasDeForma(respostasDoCorpo, grupos, admitidasDaVersao),
            .. Inscricao.RecusasDoEndereco(respostasMescladas, admitidasDaVersao),
        ];
        HashSet<string> camposRecusados = [.. recusas.Select(static r => r.Field).OfType<string>()];
        Dictionary<string, JsonElement> respostasConferiveis = respostasMescladas
            .Where(r => !camposRecusados.Contains(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

        // O grupo com id repetido ou em branco não se avalia: a identidade da ocorrência é o que a avaliação exige. A recusa de forma
        // já diz o que corrigir.
        if (recusas.Any(static r => r.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado))
        {
            return Result<ReciboDaInscricaoDto>.ValidationFailure(recusas);
        }

        // 2) O endereço pelo CEP, antes da avaliação, porque os derivados de residência dependem dele. O Geo fora do ar
        // deixa esses derivados indeterminados e a avaliação segue. A residência sai desta mesma conferência, e não de uma segunda.
        Services.ConferenciaDosEnderecos doEndereco = await Services.ConferenciaDoEnderecoDaInscricao
            .ConferirAsync(respostasConferiveis, admitidasDaVersao, conferenciaEnderecoGeo, aoEnviar: true, cancellationToken)
            .ConfigureAwait(false);
        recusas.AddRange(doEndereco.Recusas);

        Services.DerivadosCalculados derivados = Services.DerivadosDoSistemaInscricao
            .Calcular(respostasMescladas, contexto.Value!.DataReferenciaFatos, doEndereco);

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.Failure(fuso.Error!);
        }

        // 3) A avaliação da definição inteira da versão fixada (ADR-0143), a mesma do rascunho e da
        // consulta do candidato, com as etapas da inscrição concluídas: o envio é o candidato dizendo
        // que terminou. O veredito olha só o que o formulário de inscrição pergunta.
        EnvioDasRespostas envio = new(instante, fuso.Value!);
        Services.ArquivosDaInscricao arquivos = await Services.ArquivosDaInscricao
            .CarregarAsync(documentoRepository, inscricao.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<string, FatoResolvido> fatosConhecidos = derivados.Valores.ToDictionary(static d => d.Key, static d => FatoResolvido.Resolvido(d.Value), StringComparer.Ordinal);
        foreach (string indeterminado in derivados.Indeterminados)
        {
            fatosConhecidos[indeterminado] = FatoResolvido.Indeterminado();
        }

        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = Services.AvaliacaoDaInscricaoPorOpcao.Avaliar(contexto.Value!, EntradaDaAvaliacaoDaInscricao.Montar(
            Services.DerivadosDoSistemaInscricao.ComOsDerivados(admitidasDaVersao.Definicoes),
            respostasMescladas,
            Services.InscricaoRequestMapper.ParaAvaliacao(gruposMesclados),
            fatosConhecidos,
            AvaliacaoDaInscricao.CodigosDasEtapasDaInscricao(configuracao.Definicao),
            envio,
            arquivos.ApresentacoesVerificadas(),
            naFaseDaInscricao: true));
        if (avaliacao.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.Failure(avaliacao.Error!);
        }

        // 4) Os municípios dos campos que a avaliação deu como aplicáveis, conferidos no Geo.
        Services.ResultadoDaConferenciaDosMunicipios dosMunicipios = await Services.ConferenciaDosMunicipiosDaInscricao.ConferirAsync(
            configuracao.Definicao, admitidasDaVersao, avaliacao.Value!.Formulario, respostasMescladas, gruposMesclados, camposRecusados,
            conferenciaEnderecoGeo, cancellationToken).ConfigureAwait(false);

        Dictionary<string, JsonElement> respostasFinais = new(respostasMescladas, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in derivados.Valores)
        {
            respostasFinais[codigo] = valor;
        }

        // O nome social que não confere com a conta substitui a obrigatoriedade genérica do campo: a recusa
        // que orienta é a que resolve, e a genérica não diz ao candidato onde cadastrar.
        RespostasAdmitidas admitidas = admitidasDaVersao with { NomeCivilDoLogin = userContext.NomeCompleto, NomeSocialDoLogin = userContext.NomeSocial };
        IReadOnlyList<FieldError> doNomeSocial = [.. Inscricao.RecusasDoNomeSocial(respostasMescladas, admitidas, aoEnviar: true)];

        // Com o Geo fora do ar na conferência do endereço, a recusa que só existe porque um derivado de residência ficou
        // indeterminado não conta: com o Geo de volta ela pode deixar de valer. A modalidade que falta responder e deriva desses
        // derivados é uma delas; a que não concorre a nenhuma modalidade está decidida e conta.
        DomainError? geoForaDoAr = doEndereco.GeoIndisponivel;
        Services.DependenciaDoGeo? dependenciaDoGeo = geoForaDoAr is null
            ? null
            : new Services.DependenciaDoGeo(configuracao.Definicao, derivados.Indeterminados, CaminhosRespondidos(respostasMescladas, gruposMesclados));
        HashSet<string> camposComRecusaDoGeo = [.. dosMunicipios.Recusas.Select(static r => r.Field).OfType<string>()];

        // O avaliador descarta em silêncio a resposta cujo código não pertence à oferta vigente
        // (RespostaDeCampo.DentroDaOferta) — sem esta checagem sobre as respostas BRUTAS, uma opção
        // fora da oferta num campo opcional passaria sem recusa. A recusa do Geo sobre o município substitui a genérica do campo.
        // As recusas do agregado entram no acúmulo: são do envio e não dependem do Geo.
        recusas.AddRange(
        [
            .. inscricao.RecusasDoEnvio(
                respostasDoCorpo, grupos, aceites, admitidas, command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId,
                derivados.Valores, Services.DerivadosDoSistemaInscricao.DefinicoesDasRespostas, avaliacao.Value!, arquivos.EmVerificacao()),
            .. Inscricao.RecusasDasModalidades(
                dependenciaDoGeo?.SemAsModalidadesDoGeo(
                    avaliacao.Value!, contexto.Value!.ConfiguracaoDaPrimeiraOpcao, contexto.Value!.ConfiguracaoDaSegundaOpcao) ?? avaliacao.Value!,
                command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId, admitidas),
            .. Services.VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(respostasFinais, gruposMesclados, contexto.Value!.Ofertas),
            .. Services.VeredictoDeSubmissao.AvaliarDistinguindoOGeo(
                AvaliacaoDaInscricao.DaInscricao(avaliacao.Value!.Formulario), [.. contexto.Value!.TermosExigidos], aceites, envio.Dia, dependenciaDoGeo)
                .Where(r => !r.DependeDoGeo)
                .Select(static r => r.Recusa)
                .Where(e => doNomeSocial.Count == 0 || !string.Equals(e.Field, ConjuntoBasicoDaInscricao.NomeSocial, StringComparison.Ordinal))
                .Where(e => e.Field is null || !camposComRecusaDoGeo.Contains(e.Field)),
            .. dosMunicipios.Recusas,
        ]);
        recusas = [.. recusas.Distinct()];

        // 5) Resposta única: com recusa, 422 com todas; sem recusa e com o Geo fora do ar em alguma conferência, 503.
        if (recusas.Count > 0)
        {
            return Result<ReciboDaInscricaoDto>.ValidationFailure(recusas);
        }

        if ((geoForaDoAr ?? dosMunicipios.GeoIndisponivel) is { } geoIndisponivel)
        {
            return Result<ReciboDaInscricaoDto>.Failure(geoIndisponivel);
        }

        IReadOnlyList<ArquivoConfirmado> arquivosConfirmados = arquivos.Verificados();
        string numeroGerado = await geradorDeNumeroInscricao.ProximoAsync(cancellationToken).ConfigureAwait(false);
        int comprovantesJaEmitidos = (await inscricaoRepository.ObterEmissoesAsync([inscricao.Id], cancellationToken).ConfigureAwait(false)).Count;

        Result<ComprovanteInscricao> submetido = inscricao.Submeter(
            numeroGerado, command.Request.OfertaCursoId, command.Request.OfertaCursoSegundaOpcaoId, respostasDoCorpo, grupos, aceites, admitidas,
            avaliacao.Value!, derivados.Valores, Services.DerivadosDoSistemaInscricao.DefinicoesDasRespostas,
            command.Precondicao, instante, arquivosConfirmados,
            new MunicipiosConferidos(dosMunicipios.Nomes, doEndereco.Cidades, derivados.Nomes));
        if (submetido.IsFailure)
        {
            return Result<ReciboDaInscricaoDto>.ValidationFailure(submetido.Errors);
        }

        // O envio fixa os arquivos que contam e remove os de exigência que não se aplica na versão fixada;
        // no rascunho nada foi apagado. Os objetos do armazenamento saem pela rotina de limpeza, depois do commit.
        await Services.DocumentosEnviadosDaInscricao
            .ConciliarNoEnvioAsync(documentoRepository, arquivos, avaliacao.Value!, arquivosConfirmados, instante, cancellationToken).ConfigureAwait(false);

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
            return Result<ReciboDaInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.ConflitoDeConcorrencia, "Esta inscrição foi alterada concorrentemente. Tente novamente."));
        }

        return Result<ReciboDaInscricaoDto>.Success(ReciboDe(inscricao, submetido.Value!, comprovantesJaEmitidos + 1));
    }

    /// <summary>O recibo do envio: o suficiente para abrir o comprovante emitido, que ocupa a posição seguinte às já emitidas.</summary>
    private static ReciboDaInscricaoDto ReciboDe(Inscricao inscricao, ComprovanteInscricao comprovante, int numeroDoComprovante) =>
        new(inscricao.Id, comprovante.NumeroInscricao, numeroDoComprovante, comprovante.Instante);

    /// <summary>Os caminhos dos campos que o candidato respondeu: <c>FATO</c> no nível superior e <c>GRUPO.ocorrencia.FATO</c> nos grupos.</summary>
    private static HashSet<string> CaminhosRespondidos(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
    [
        .. respostas.Where(static r => !RespostaDeCampo.EstaVazia(r.Value)).Select(static r => r.Key),
        .. grupos.SelectMany(static g => g.Value.SelectMany(o => o.Respostas
            .Where(static r => !RespostaDeCampo.EstaVazia(r.Value))
            .Select(r => $"{g.Key}.{o.Id}.{r.Key}"))),
    ];
}
