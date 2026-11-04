namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

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
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // A recópia das cópias do item altera o agregado rastreado antes dos gates seguintes; a
        // recusa não devolve exceção, então o que ela mudou não pode ir para o banco.
        return PreparoDoCongelamento.SemPersistirEmRecusaAsync(
            unitOfWork,
            () => Executar(command, processoSeletivoRepository, documentoAPublicarRepository, rascunhoDePublicacaoRepository, atoDoProcessoRepository, canonicalizer, resolvedorFuso, unitOfWork, userContext, tipoDeAtoReader, vagaDeLinhagemReader, obrigatoriedadeLegalRepository, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, tipoDeficienciaReader, regraCatalogoReader, fatoCandidatoReader, calendarioVigenteReader, unidadeResponsavelResolvedor, timeProvider, cancellationToken));
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
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
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
                command.ProcessoSeletivoId, command.DocumentoEditalId, command.Ato, "na publicação", cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoAto.IsFailure)
        {
            return (Result.Failure(conferenciaDoAto.Error!), []);
        }

        (DocumentoAPublicar documento, TipoAtoPublicadoView tipoConferido, IReadOnlyList<Guid> atosDaLinhagem) = conferenciaDoAto.Value;

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

        Result<ProtecaoVigenteDoProcesso> preparo = await PreparoDoCongelamento
            .PrepararAsync(processo, catalogo, null, null, unidadeResponsavelResolvedor, cancellationToken)
            .ConfigureAwait(false);
        if (preparo.IsFailure)
        {
            return (Result.ValidationFailure(preparo.Errors), []);
        }

        ProtecaoVigenteDoProcesso protecaoDoProcesso = preparo.Value!;
        ProtecaoVigenteDasExigencias protecaoDasExigencias = protecaoDoProcesso.Exigencias;

        // O gate precede a canonicalização (ADR-0109 D5): um processo não conforme
        // não chega a ser projetado. Sem isso, a canonicalização de uma dimensão
        // obrigatória ausente falharia alto (D8) em vez de devolver o DomainError
        // que o contrato HTTP promete. A raiz reavalia — este é o guarda antecipado,
        // não a autoridade.
        if (ChecklistDeConformidade.PendenciaDeConformidade(EstadoDaConfiguracao.De(processo)) is { } pendencia)
        {
            return (Result.Failure(pendencia), []);
        }

        // Cascata de remanejamento de cotas (UNI-REQ-0056, Story #575) — cross-dimensão,
        // fora do agregador genérico acima (o item "Cascata de remanejamento" nunca
        // entra em PendenciaDeConformidade, só em PendenciaDaCascata). Mesma antecipação
        // e mesmo motivo das demais (ADR-0109 D5).
        if (CoberturaDaCascata.PendenciaDaCascata(EstadoDaConfiguracao.De(processo)) is { } pendenciaCascata)
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
        if (PendenciasDoCronograma.PendenciaDoCronograma(EstadoDaConfiguracao.De(processo), fusoInstitucional) is { } pendenciaCronograma)
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
        if (ProntidaoParaCongelar.PendenciaPreCanonicalizacao(EstadoDaConfiguracao.De(processo), fatosDeModalidade, protecaoDasExigencias) is { } pendenciaPreCanonicalizacao)
        {
            return (Result.Failure(pendenciaPreCanonicalizacao), []);
        }

        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadadosFatosResult =
            ConferenciaFinalDosFatos.Conferir(processo, catalogoPorCodigo);
        if (metadadosFatosResult.IsFailure)
        {
            return (Result.Failure(metadadosFatosResult.Error!), []);
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

        string atorUsuarioSub = userContext.UserId ?? "system";

        PedidoDeCongelamento pedido = await MontagemDoCongelamento
            .MontarAsync(
                calendarioVigenteReader, canonicalizer, timeProvider,
                new EntradaCanonicalizacao(
                    processo, dados, documento.HashSha256!, fusoResult.Value!.Id,
                    Conformidade: conformidadeLegal.Value,
                    MetadadosFatosCongelados: metadadosFatosResult.Value,
                    ValoresSelecionaveisCongelados: valoresSelecionaveisResult.Value,
                    AgregadosDosGrupos: DefinicaoAvaliavelDoProcesso.AgregadosDosGrupos(processo.GruposColetados, catalogoPorCodigo),
                    ApresentacaoDosPressupostos: avaliavel.Value!.ApresentacaoDosPressupostos,
                    TiposDeAtoCongelados: tiposDeAtoCongelados.Value,
                    FatosDerivadosCongelados: protecaoDoProcesso.FatosDerivados),
                fusoInstitucional, atorUsuarioSub, fatosDeModalidade, tiposDeAtoUnicos, cancellationToken)
            .ConfigureAwait(false);

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(pedido);
        if (publicarResult.IsFailure)
        {
            return (Result.Failure(publicarResult.Error!), []);
        }

        return await GravacaoDaVersaoCongelada
            .GravarAsync(
                new VersaoCongelada(processo, publicarResult.Value!, command.Ato, tipoConferido, dados, documento, AtoRetificadoId: null, MotivoRetificacao: null),
                processoSeletivoRepository, atoDoProcessoRepository, rascunhoDePublicacaoRepository, unitOfWork, timeProvider, cancellationToken)
            .ConfigureAwait(false);
    }
}
