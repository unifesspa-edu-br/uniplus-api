namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler convention-based do <see cref="PublicarProcessoSeletivoCommand"/>
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019, Story #759 T4 #785, ADR-0005 + ADR-0041): carrega o agregado,
/// valida o documento confirmado (T3), canonicaliza a configuração
/// (ADR-0100) e delega a orquestração de negócio a
/// <see cref="ProcessoSeletivo.Publicar"/>. Cascading messages — o
/// <see cref="Domain.Events.ProcessoPublicadoEvent"/> é drenado só depois do
/// <c>SaveChanges</c> bem-sucedido.
/// </summary>
public static class PublicarProcessoSeletivoCommandHandler
{
    public static Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarProcessoSeletivoCommand command,
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
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // A recópia das cópias do item altera o agregado rastreado antes dos gates seguintes; a
        // recusa não devolve exceção, então o que ela mudou não pode ir para o banco.
        return ReconferenciaDasCopiasDoItem.SemPersistirEmRecusaAsync(
            unitOfWork,
            () => Executar(command, processoSeletivoRepository, documentoAPublicarRepository, rascunhoDePublicacaoRepository, atoDoProcessoRepository, canonicalizer, resolvedorFuso, unitOfWork, userContext, tipoDeAtoReader, vagaDeLinhagemReader, obrigatoriedadeLegalRepository, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, fatoCandidatoReader, calendarioVigenteReader, timeProvider, cancellationToken));
    }

    private static async Task<(Result Resposta, IEnumerable<object> Eventos)> Executar(
        PublicarProcessoSeletivoCommand command,
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

        Result<DocumentoAPublicar> conferenciaDoDocumento = await ConferenciaDoDocumentoDoEdital
            .ConferirAsync(documentoAPublicarRepository, command.ProcessoSeletivoId, command.DocumentoEditalId, "na publicação", cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoDocumento.IsFailure)
        {
            return (Result.Failure(conferenciaDoDocumento.Error!), []);
        }

        DocumentoAPublicar documento = conferenciaDoDocumento.Value!;

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

        // Uma leitura só do catálogo (D4-bis), compartilhada pelos gates que dependem dos fatos
        // cujos valores são modalidades, pela reconferência de coletabilidade e pelos resolvedores
        // que congelam vocabulário de fato — duas leituras abririam janela para uma conferência
        // aprovar sobre um catálogo e um resolvedor congelar sobre outro.
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, FatoCandidatoView> catalogoPorCodigo =
            catalogoDeFatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        FatosDeModalidade fatosDeModalidade = VocabularioDeFatos.ComValoresDeModalidade(catalogoDeFatos);

        // O catálogo é editável depois que o item copiou dele a origem dos valores, o formato e a
        // proteção, e os gates abaixo avaliam a cópia: ela é reconferida e recopiada antes deles.
        Result copiasDoItem = ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogoDeFatos, null);
        if (copiasDoItem.IsFailure)
        {
            return (copiasDoItem, []);
        }

        // O tipo de documento é editável depois que a exigência copiou dele a proteção: o valor
        // vigente é o que se congela, antes da conferência contra os fatos e do snapshot.
        ProtecaoVigenteDasExigencias protecaoDasExigencias = await ProtecaoVigenteDasExigenciasDoProcesso
            .MontarAsync(processo, tipoDocumentoReader, catalogoDeFatos, null, cancellationToken)
            .ConfigureAwait(false);
        processo.AplicarProtecaoVigente(protecaoDasExigencias);

        // O gate precede a canonicalização (ADR-0109 D5): um processo não conforme
        // não chega a ser projetado. Sem isso, a canonicalização de uma dimensão
        // obrigatória ausente falharia alto (D8) em vez de devolver o DomainError
        // que o contrato HTTP promete. A raiz reavalia — este é o guarda antecipado,
        // não a autoridade.
        if (processo.PendenciaDeConformidade() is { } pendencia)
        {
            return (Result.Failure(pendencia), []);
        }

        // Cascata de remanejamento de cotas (UNI-REQ-0056, Story #575) — cross-dimensão,
        // fora do agregador genérico acima (o item "Cascata de remanejamento" nunca
        // entra em PendenciaDeConformidade, só em PendenciaDaCascata). Mesma antecipação
        // e mesmo motivo das demais (ADR-0109 D5).
        if (processo.PendenciaDaCascata() is { } pendenciaCascata)
        {
            return (Result.Failure(pendenciaCascata), []);
        }

        // Antecipado: a conferência legal abaixo precisa do fuso para derivar o dia civil do
        // início da inscrição. Falha aqui é defeito de instalação (500), não gate (issue #1350).
        Result<TimeZoneInfo> fusoResult = resolvedorFuso.Resolver();
        if (fusoResult.IsFailure)
        {
            return (Result.Failure(fusoResult.Error!), []);
        }

        TimeZoneInfo fusoInstitucional = fusoResult.Value!;

        // Depois da resolução do fuso: a janela de isenção conta cinco dias corridos, e o quinto
        // dia só se completa no último instante do dia no fuso institucional.
        if (processo.PendenciaDoCronograma(fusoInstitucional) is { } pendenciaCronograma)
        {
            return (Result.Failure(pendenciaCronograma), []);
        }

        Result<TiposDeAtoUnicosPorObjeto> tiposUnicos = await ResolucaoDosTiposDeAtoUnicos
            .ConferirAsync(tipoDeAtoReader, atoDoProcessoRepository, processo, tipoConferido, atosDaLinhagem, timeProvider, fusoInstitucional, new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal), cancellationToken)
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
            .ConferirAsync(documentoAPublicarRepository, command.ProcessoSeletivoId, command.Anexos, [], cancellationToken)
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

        // Terceira dimensão de conformidade (Story #554, PR #903, ADR-0109 D5): documentos
        // exigidos, coerência da consequência de indeferimento e referência temporal de
        // fatos. A canonicalização abaixo resolve dataReferenciaFatos internamente e LANÇA
        // quando a política não resolve — sem este guard antes dela, um processo inválido
        // vira exceção não tratada em vez do DomainError que o contrato HTTP promete.
        if (processo.PendenciaPreCanonicalizacao(fatosDeModalidade, protecaoDasExigencias) is { } pendenciaPreCanonicalizacao)
        {
            return (Result.Failure(pendenciaPreCanonicalizacao), []);
        }


        // O catálogo pode reclassificar a Origem de um fato depois que ele já virou
        // FatoColetado (ex.: a migration que reclassificou MODALIDADE de DECLARADO para
        // DERIVADO) — sem esta reconferência, um vínculo morto seria congelado numa versão
        // append-only e, pela doutrina do agregado, irreparável depois.
        Result coletabilidadeDosFatos = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogoPorCodigo);
        if (coletabilidadeDosFatos.IsFailure)
        {
            return (Result.Failure(coletabilidadeDosFatos.Error!), []);
        }

        // Story #919 (congelamento dos parâmetros na publicação, UNI-REQ-0019): congela o metadado de cada fato citado em alguma condição de
        // gatilho, ao lado da condição bruta {fato, operador, valor} já congelada desde a 1.2.
        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadadosFatosResult =
            ResolvedorMetadadosFatosCongelados.Resolver(processo, catalogoPorCodigo);
        if (metadadosFatosResult.IsFailure)
        {
            return (Result.Failure(metadadosFatosResult.Error!), []);
        }

        // Os fatos dos gatilhos continuam no processo e conhecidos até a fase de cada exigência:
        // itens, formulários e derivações mudam depois que a exigência os citou.
        if (ConferenciaDosFatosDosGatilhos.Conferir(processo, catalogoPorCodigo) is { } gatilhoIncoerente)
        {
            return (Result.Failure(gatilhoIncoerente), []);
        }

        if (ConferenciaDoFatoDeElegibilidade.Conferir(processo, catalogoPorCodigo) is { } elegibilidadeIncoerente)
        {
            return (Result.Failure(elegibilidadeIncoerente), []);
        }

        // Story #1059 (UNI-REQ-0072): os valores que o candidato pode escolher para cada fato de
        // seleção coletado — do domínio estático do catálogo ou da oferta do próprio processo.
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> valoresSelecionaveisResult =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogoPorCodigo);
        if (valoresSelecionaveisResult.IsFailure)
        {
            return (Result.Failure(valoresSelecionaveisResult.Error!), []);
        }

        // Os fatos que as regras citam e nenhum formulário pergunta saem com o rótulo e o tipo do
        // catálogo, congelados na versão; o fato citado sem cadastro recusa aqui, antes de canonicalizar.
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, catalogoDeFatos);
        if (avaliavel.IsFailure)
        {
            return (Result.Failure(avaliavel.Error!), []);
        }

        // UNI-REQ-0116: UMA leitura do calendário vigente por operação. A mesma resposta
        // alimenta o gate da raiz e o bloco congelado do envelope — ler duas vezes abriria a
        // janela em que o dataset muda entre validar e congelar, e a versão publicada
        // carregaria um calendário que o gate não aprovou.
        CalendarioVigenteView? calendarioVigente = await calendarioVigenteReader
            .ObterVigenteAsync(cancellationToken)
            .ConfigureAwait(false);
        // Dataset vigente incoerente não aborta aqui: quem decide se ele importa é o gate da
        // raiz, e só importa para processo cuja contagem distingue dia útil. Abortar antes
        // recusaria também quem não usa o dado que está quebrado, e o checklist ficaria verde
        // para um processo que a publicação recusa.
        Result<CalendarioDiasUteisCongelado?> calendarioResult =
            LeituraDoCalendarioVigente.Traduzir(calendarioVigente);

        var contexto = new ContextoDeContagemDePrazos(
            calendarioResult.IsSuccess ? calendarioResult.Value : null,

            FalhaDoCalendarioVigente: calendarioResult.IsFailure ? calendarioResult.Error : null,
            FusoInstitucional: fusoInstitucional);

        SnapshotCanonico canonico = canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(
                processo, dados, documento.HashSha256!, fusoResult.Value!.Id,
                Conformidade: conformidadeLegal.Value,
                MetadadosFatosCongelados: metadadosFatosResult.Value,
                ValoresSelecionaveisCongelados: valoresSelecionaveisResult.Value,
                CalendarioDiasUteis: contexto.CalendarioVigente,
                AgregadosDosGrupos: DefinicaoAvaliavelDoProcesso.AgregadosDosGrupos(processo.GruposColetados, catalogoPorCodigo),
                ApresentacaoDosPressupostos: avaliavel.Value!.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: tiposDeAtoCongelados.Value));

        string atorUsuarioSub = userContext.UserId ?? "system";

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dados,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub,
            timeProvider,
            contexto,
            fatosDeModalidade,
            tiposDeAtoUnicos);
        if (publicarResult.IsFailure)
        {
            return (Result.Failure(publicarResult.Error!), []);
        }

        VersaoConfiguracao versao = publicarResult.Value!;

        // A recusa vem antes de qualquer escrita (ADR-0125).
        Result<AtoDoProcesso> registroDoAto = RegistroDoAtoDaVersao.Montar(
            processo,
            versao,
            command.Ato,
            tipoConferido,
            dados,
            documento.Id,
            atoRetificadoId: null,
            motivoRetificacao: null,
            timeProvider);
        if (registroDoAto.IsFailure)
        {
            return (Result.ValidationFailure(registroDoAto.Errors), []);
        }

        await processoSeletivoRepository
            .AdicionarVersaoConfiguracaoAsync(versao, cancellationToken)
            .ConfigureAwait(false);

        // O registro do ato vai na mesma gravação da versão e do envio ao outbox: a consulta dos atos
        // do processo lê só ele, e a confirmação do Publicações o encontra pelo id do ato.
        await atoDoProcessoRepository.AdicionarAsync(registroDoAto.Value!, cancellationToken).ConfigureAwait(false);

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

            // A recusa não devolve exceção: o envoltório de Handle descarta o rastreamento antes de o
            // Wolverine salvar de novo (ADR-0119).
            return (Result.Failure(erroVersao), []);
        }

        // Só com a versão gravada o rascunho perde a razão de existir, e só aqui sai do banco
        // o nome de quem assinou. Apagar antes do flush punha a transcrição do operador numa
        // aposta: ApagarDoProcessoAsync emite o DELETE na hora, e qualquer recusa daí em
        // diante devolveria "nada foi publicado" com o bloco transcrito do Diário Oficial
        // destruídos — inclusive o rascunho do colega, que a exclusão alcança por ser por
        // processo. Todo caminho que registra ato apaga: nenhum ganha isso de graça, porque a
        // raiz é soft-deletable e o cascade da chave estrangeira jamais dispara.
        await rascunhoDePublicacaoRepository
            .ApagarDoProcessoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

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
