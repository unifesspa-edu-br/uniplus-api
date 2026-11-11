namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

using Queries.ProcessosSeletivos;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// Handler convention-based do <see cref="RetificarProcessoSeletivoCommand"/>
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0101 + ADR-0005): carrega o agregado com a
/// cadeia de Editais, valida o documento confirmado (T3), canonicaliza a
/// configuração acrescida do bloco de retificação (ADR-0100/0101) e delega a
/// orquestração de negócio a <see cref="ProcessoSeletivo.Retificar"/>.
/// Cascading messages — o <see cref="Domain.Events.ProcessoPublicadoEvent"/>
/// só é drenado depois do <c>SaveChanges</c> bem-sucedido.
/// </summary>
public static class RetificarProcessoSeletivoCommandHandler
{
    public static Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        RetificarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IRascunhoDePublicacaoRepository rascunhoDePublicacaoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        IResolvedorFusoInstitucional resolvedorFuso,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ICalendarioVigenteReader calendarioVigenteReader,
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // A recópia das cópias do item altera o agregado rastreado antes dos gates seguintes; a
        // recusa não devolve exceção, então o que ela mudou não pode ir para o banco.
        return RecopiaAoCongelar.SemPersistirEmRecusaAsync(
            unitOfWork,
            () => Executar(command, processoSeletivoRepository, documentoAPublicarRepository, rascunhoDePublicacaoRepository, atoDoProcessoRepository, canonicalizer, resolvedorFuso, unitOfWork, userContext, tipoDeAtoReader, vagaDeLinhagemReader, obrigatoriedadeLegalRepository, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, fatoCandidatoReader, calendarioVigenteReader, unidadeResponsavelResolvedor, motivoDoProcessoRepository, registroCodecs, timeProvider, cancellationToken));
    }

    private static async Task<(Result Resposta, IEnumerable<object> Eventos)> Executar(
        RetificarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IRascunhoDePublicacaoRepository rascunhoDePublicacaoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        IResolvedorFusoInstitucional resolvedorFuso,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ICalendarioVigenteReader calendarioVigenteReader,
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(rascunhoDePublicacaoRepository);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(calendarioVigenteReader);
        ArgumentNullException.ThrowIfNull(unidadeResponsavelResolvedor);
        ArgumentNullException.ThrowIfNull(motivoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
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

        Result<AtoEDocumentoConferidos> conferenciaDoAto = await ConferenciaDoAtoEDoDocumento
            .ConferirAsync(
                documentoAPublicarRepository, processoSeletivoRepository, tipoDeAtoReader, vagaDeLinhagemReader,
                command.ProcessoSeletivoId, command.DocumentoEditalId, command.Ato, "na retificação", cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoAto.IsFailure)
        {
            return (Result.Failure(conferenciaDoAto.Error!), []);
        }

        (DocumentoAPublicar documento, TipoAtoPublicadoView tipoConferido, IReadOnlyList<Guid> atosDaLinhagem) = conferenciaDoAto.Value;

        Result<VersaoConfiguracao> linhagem = await ConferenciaDaLinhagemRetificavel
            .ConferirAsync(
                processoSeletivoRepository, vagaDeLinhagemReader, atoDoProcessoRepository, registroCodecs,
                processo, versaoBaseEsperada: null, cancellationToken)
            .ConfigureAwait(false);
        if (linhagem.IsFailure)
        {
            return (Result.ValidationFailure(linhagem.Errors), []);
        }

        VersaoConfiguracao versaoAtual = linhagem.Value!;

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

        Result<CatalogoConferido> conferenciaDoCatalogo = await ConferenciaDoCatalogoEDaProtecao
            .ConferirAsync(processo, fatoCandidatoReader, tipoDocumentoReader, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoCatalogo.IsFailure)
        {
            return (Result.ValidationFailure(conferenciaDoCatalogo.Errors), []);
        }

        CatalogoConferido catalogo = conferenciaDoCatalogo.Value!;
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos = catalogo.Fatos;
        IReadOnlyDictionary<string, FatoCandidatoView> catalogoPorCodigo = catalogo.PorCodigo;
        FatosDeModalidade fatosDeModalidade = catalogo.FatosDeModalidade;

        Result<FatosDasVersoesPublicadas?> versoesPublicadas = await ConferenciaDosEixosDosFatosColetados
            .LerAsync(processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versoesPublicadas.IsFailure)
        {
            return (Result.Failure(versoesPublicadas.Error!), []);
        }

        Result<ProtecaoVigenteDoProcesso> recopia = await RecopiaAoCongelar
            .RecopiarAsync(processo, catalogo, versoesPublicadas.Value?.FatosJaColetados, await ProtecaoVigenteDasExigenciasDoProcesso.LerDaVersaoVigenteAsync(processoSeletivoRepository, registroCodecs, processo.Id, cancellationToken).ConfigureAwait(false), unidadeResponsavelResolvedor, cancellationToken)
            .ConfigureAwait(false);
        if (recopia.IsFailure)
        {
            return (Result.ValidationFailure(recopia.Errors), []);
        }

        ProtecaoVigenteDoProcesso protecaoDoProcesso = recopia.Value!;
        ProtecaoVigenteDasExigencias protecaoDasExigencias = protecaoDoProcesso.Exigencias;

        // O gate precede a canonicalização, igual à publicação (ADR-0109 D5) — a
        // retificação também congela uma versão append-only e vinculante.
        if (ChecklistDeConformidade.PendenciaDeConformidade(EstadoDaConfiguracao.De(processo)) is { } pendencia)
        {
            return (Result.Failure(pendencia), []);
        }

        // Cascata de remanejamento de cotas (UNI-REQ-0056) — cross-dimensão,
        // fora do agregador genérico acima. Mesma antecipação da publicação (ADR-0109 D5).
        if (CoberturaDaCascata.PendenciaDaCascata(EstadoDaConfiguracao.De(processo)) is { } pendenciaCascata)
        {
            return (Result.Failure(pendenciaCascata), []);
        }

        // Antecipado: a conferência legal abaixo precisa do fuso para derivar o dia civil do
        // início da inscrição. Falha aqui é defeito de instalação (500), não gate.
        Result<TimeZoneInfo> fusoResult = resolvedorFuso.Resolver();
        if (fusoResult.IsFailure)
        {
            return (Result.Failure(fusoResult.Error!), []);
        }

        TimeZoneInfo fusoInstitucional = fusoResult.Value!;

        // Depois da resolução do fuso: a janela de isenção conta cinco dias corridos, e o quinto
        // dia só se completa no último instante do dia no fuso institucional.
        if (PendenciasDoCronograma.PendenciaDoCronograma(EstadoDaConfiguracao.De(processo), fusoInstitucional) is { } pendenciaCronograma)
        {
            return (Result.Failure(pendenciaCronograma), []);
        }

        // O tipo que a versão vigente já congelou mantém a classificação dela: mudar o catálogo depois
        // não alcança o processo já publicado, nem pela retificação.
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> jaCongelados = CongelamentoDosTiposDeAto.DaVersao(registroCodecs, versaoAtual);
        if (jaCongelados.IsFailure)
        {
            return (Result.Failure(jaCongelados.Error!), []);
        }

        Result<TiposDeAtoUnicosPorObjeto> tiposUnicos = await ResolucaoDosTiposDeAtoUnicos
            .ConferirAsync(tipoDeAtoReader, atoDoProcessoRepository, processo, tipoConferido, atosDaLinhagem, timeProvider, fusoInstitucional, jaCongelados.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (tiposUnicos.IsFailure)
        {
            return (Result.Failure(tiposUnicos.Error!), []);
        }

        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos = tiposUnicos.Value!;

        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> tiposDeAtoCongelados = CongelamentoDosTiposDeAto.Resolver(processo, tiposDeAtoUnicos);
        if (tiposDeAtoCongelados.IsFailure)
        {
            return (Result.Failure(tiposDeAtoCongelados.Error!), []);
        }

        Result<IReadOnlyList<AnexoDoEdital>> conferenciaDosAnexos = await ConferenciaDosAnexosDoEdital
            .ConferirAsync(documentoAPublicarRepository, command.ProcessoSeletivoId, command.Anexos, LeituraDoCertameEdital.AnexosDaVersao(versaoAtual, herdaOsDaVersao: command.Anexos is null), cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDosAnexos.IsFailure)
        {
            return (Result.Failure(conferenciaDosAnexos.Error!), []);
        }

        Result<DadosEdital> dadosResult = ResolucaoDoPeriodoDeInscricao.Resolver(
            processo,
            command.Numero,
            command.PeriodoInscricaoInicio,
            command.PeriodoInscricaoFim,
            command.DocumentoEditalId,
            conferenciaDosAnexos.Value!);
        if (dadosResult.IsFailure)
        {
            return (Result.Failure(dadosResult.Error!), []);
        }

        DadosEdital dados = dadosResult.Value!;

        Result<ResultadoConformidade> conformidadeLegal = await ConferenciaDeConformidadeLegal
            .AvaliarAsync(obrigatoriedadeLegalRepository, processo, dados.DiaDeReferenciaLegal(fusoInstitucional), modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, fatosDeModalidade, cancellationToken)
            .ConfigureAwait(false);
        if (conformidadeLegal.IsFailure)
        {
            return (Result.Failure(conformidadeLegal.Error!), []);
        }

        // Terceira dimensão de conformidade (ADR-0109 D5): documentos
        // exigidos, coerência da consequência de indeferimento e referência temporal de
        // fatos. A canonicalização abaixo resolve dataReferenciaFatos internamente e LANÇA
        // quando a política não resolve — sem este guard antes dela, um processo inválido
        // vira exceção não tratada em vez do DomainError que o contrato HTTP promete.
        if (ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), fatosDeModalidade, protecaoDasExigencias) is { } pendenciaPreCanonicalizacao)
        {
            return (Result.Failure(pendenciaPreCanonicalizacao), []);
        }

        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadadosFatosResult =
            ConferenciaDosFatosAoCongelar.Conferir(processo, catalogoPorCodigo);
        if (metadadosFatosResult.IsFailure)
        {
            return (Result.Failure(metadadosFatosResult.Error!), []);
        }

        // UNI-REQ-0072: os valores que o candidato pode escolher para cada fato de
        // seleção coletado — mesmo raciocínio da publicação de abertura.
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> valoresSelecionaveisResult =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogoPorCodigo);
        if (valoresSelecionaveisResult.IsFailure)
        {
            return (Result.Failure(valoresSelecionaveisResult.Error!), []);
        }

        // O metadado dos fatos dos gatilhos e a operação dos agregados vêm do catálogo na hora de
        // congelar: o fato já coletado por versão publicada não os troca (UNI-REQ-0144).
        IReadOnlyList<DefinicaoAgregado> agregadosDosGrupos =
            DefinicaoAvaliavelDoProcesso.AgregadosDosGrupos(processo.GruposColetados, catalogoPorCodigo);
        if (versoesPublicadas.Value is { } publicadas
            && ConferenciaDosEixosDosFatosColetados.Conferir(publicadas, metadadosFatosResult.Value, agregadosDosGrupos) is { } eixoTrocado)
        {
            return (Result.Failure(eixoTrocado), []);
        }

        // Os fatos que as regras citam e nenhum formulário pergunta saem com o rótulo e o tipo do
        // catálogo, congelados na versão; o fato citado sem cadastro recusa aqui, antes de canonicalizar.
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, catalogoDeFatos);
        if (avaliavel.IsFailure)
        {
            return (Result.Failure(avaliavel.Error!), []);
        }

        // Último portão, depois de todos os demais: os motivos escolhidos vêm de motivo ativo e cobrem
        // as decisões do processo (UNI-REQ-0123); só então a escolha congela.
        Result motivos = await ConferenciaDosMotivosAoCongelar.ConferirAsync(motivoDoProcessoRepository, processo, cancellationToken).ConfigureAwait(false);
        if (motivos.IsFailure)
        {
            return (Result.ValidationFailure(motivos.Errors), []);
        }

        string atorUsuarioSub = userContext.UserId ?? "system";

        PedidoDeCongelamento pedido = await MontagemDoCongelamento
            .MontarAsync(
                calendarioVigenteReader, canonicalizer, timeProvider,
                new EntradaCanonicalizacao(
                    processo, dados, documento.HashSha256!, fusoResult.Value!.Id,
                    new RetificacaoInfo(versaoAtual.AtoCriadorId, motivo),
                    conformidadeLegal.Value,
                    metadadosFatosResult.Value,
                    valoresSelecionaveisResult.Value,
                    CalendarioDiasUteis: null,
                    agregadosDosGrupos,
                    ApresentacaoDosPressupostos: avaliavel.Value!.ApresentacaoDosPressupostos,
                    TiposDeAtoCongelados: tiposDeAtoCongelados.Value,
                    FatosDerivadosCongelados: protecaoDoProcesso.FatosDerivados),
                fusoInstitucional, atorUsuarioSub, fatosDeModalidade, tiposDeAtoUnicos, cancellationToken)
            .ConfigureAwait(false);

        Result<VersaoConfiguracao> retificarResult = processo.Retificar(pedido, versaoAtual, motivo);
        if (retificarResult.IsFailure)
        {
            return (Result.Failure(retificarResult.Error!), []);
        }

        return await GravacaoDaVersaoCongelada
            .GravarAsync(
                new VersaoCongelada(processo, retificarResult.Value!, command.Ato, tipoConferido, dados, documento, versaoAtual.AtoCriadorId, motivo),
                processoSeletivoRepository, atoDoProcessoRepository, rascunhoDePublicacaoRepository, unitOfWork, timeProvider, cancellationToken)
            .ConfigureAwait(false);
    }
}
