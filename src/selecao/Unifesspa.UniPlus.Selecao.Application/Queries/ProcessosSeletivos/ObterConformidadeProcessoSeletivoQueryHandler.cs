namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// Handler da <see cref="ObterConformidadeProcessoSeletivoQuery"/>: leitura
/// pura (sem side effects) que mapeia <see cref="ProcessoSeletivo.AvaliarConformidade"/>
/// para o DTO público — não confundir com a conformidade de
/// <c>ObrigatoriedadeLegal</c> (Stories #852/#853), que avalia regras
/// legais configuráveis aplicáveis ao processo.
/// </summary>
/// <remarks>
/// O checklist em si vive em <c>ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(ProcessoSeletivo))</c> (Domain) — bicondicional
/// com os SEIS gates estruturais que <c>Publicar</c>/<c>Retificar</c> aplicam (issue #1092), não
/// só o primeiro. Este handler apenas mapeia; a fonte única evita que a leitura pública e os gates
/// de publicação divirjam.
/// <para>
/// <b>O contexto que o agregado não tem.</b> Duas das causas projetadas não estão no agregado: o
/// calendário vigente é do módulo Configuração, e o reconhecimento do fuso institucional depende
/// da base de fusos do runtime. As duas são resolvidas aqui e passadas prontas — o Domain não lê
/// reader nem serviço (ADR-0042). Sem elas, o checklist teria de inferir o que não sabe, e
/// devolveria verde para processo que a publicação recusa.
/// </para>
/// <para>
/// <b>Uma leitura por handler, não por requisição.</b> Este handler lê o calendário uma vez.
/// Numa recusa de publicação, o controller o consulta de novo para enriquecer o 422 com o
/// checklist (issue #1096) — são dois handlers, cada um com a sua leitura, e isso é correto: a
/// invariante de leitura única é do handler, não do endpoint.
/// </para>
/// <para>
/// <b>Avisos.</b> Os avisos vêm de <c>ChecklistDeConformidade.AvaliarAvisos(EstadoDaConfiguracao.De(ProcessoSeletivo))</c> e viajam em lista
/// própria: só esta consulta os calcula, nunca os gates de publicação e retificação.
/// </para>
/// </remarks>
public static class ObterConformidadeProcessoSeletivoQueryHandler
{
    /// <summary>
    /// Item do checklist para a versão publicada que o codec não lê: sem ela não há classificação
    /// congelada para conferir, e a retificação recusaria o processo pelo mesmo motivo.
    /// </summary>
    internal const string CodigoDaVersaoPublicadaIlegivel = "cronograma_versao_publicada_legivel";

    public static async Task<ConformidadeProcessoSeletivoDto?> Handle(
        ObterConformidadeProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        LeituraDoContextoDeContagemDePrazos contextos,
        ResolucaoDosTiposDeAtoUnicos tiposDeAto,
        ConferenciaDoCatalogoEDaProtecao catalogos,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(contextos);
        ArgumentNullException.ThrowIfNull(tiposDeAto);
        ArgumentNullException.ThrowIfNull(catalogos);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return processo is null
            ? null
            : await AvaliarAsync(processo, processoSeletivoRepository, contextos, tiposDeAto, catalogos, registroCodecs, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ConformidadeProcessoSeletivoDto> AvaliarAsync(
        ProcessoSeletivo processo,
        IProcessoSeletivoRepository processoSeletivoRepository,
        LeituraDoContextoDeContagemDePrazos contextos,
        ResolucaoDosTiposDeAtoUnicos tiposDeAto,
        ConferenciaDoCatalogoEDaProtecao catalogos,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ContextoDeContagemDePrazos contexto = await contextos.LerAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Guid> atosDaLinhagem = await processoSeletivoRepository
            .ObterAtosCriadoresAsync(processo.Id, cancellationToken)
            .ConfigureAwait(false);
        VersaoPublicada publicada = await LerAVersaoPublicadaAsync(processo.Id, processoSeletivoRepository, registroCodecs, cancellationToken).ConfigureAwait(false);
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos = await tiposDeAto
            .ParaChecklistAsync(processo, atosDaLinhagem, contexto.FusoInstitucional, publicada.TiposCongelados, cancellationToken)
            .ConfigureAwait(false);
        CatalogoVivo catalogo = await catalogos.LerAsync(processo, cancellationToken).ConfigureAwait(false);

        return Projetar(processo, contexto, tiposDeAtoUnicos, catalogo, publicada);
    }

    // Como a retificação, o checklist trata o tipo que a versão vigente já congelou pela classificação congelada; só o
    // processo ainda não publicado, ou o tipo novo, lê o catálogo de hoje. Uma versão que o codec não lê é recusada pela
    // própria retificação, e o checklist mostra essa recusa como pendência em vez de avaliar os tipos pelo catálogo de
    // hoje e parecer conforme.
    private static async Task<VersaoPublicada> LerAVersaoPublicadaAsync(
        Guid processoId, IProcessoSeletivoRepository repositorio, IRegistroCodecsEnvelope registroCodecs, CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versaoAtual = await repositorio.ObterVersaoAtualAsync(processoId, cancellationToken).ConfigureAwait(false);
        if (versaoAtual is null)
        {
            return new VersaoPublicada(new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal), null, null);
        }

        Result<EnvelopeReidratado> envelope = registroCodecs.Reidratar(versaoAtual);
        Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> congelados = CongelamentoDosTiposDeAto.DoEnvelope(envelope);

        return new VersaoPublicada(
            congelados.IsSuccess ? congelados.Value! : new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal),
            ProtecaoVigenteDasExigenciasDoProcesso.DoEnvelope(envelope),
            congelados.Error);
    }

    private static ConformidadeProcessoSeletivoDto Projetar(
        ProcessoSeletivo processo,
        ContextoDeContagemDePrazos contexto,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos,
        CatalogoVivo catalogo,
        VersaoPublicada publicada)
    {
        ItemConformidadeDto[] itens =
        [
            .. ItensDoChecklist(processo, contexto, tiposDeAtoUnicos, catalogo, publicada),
            .. ConferenciaDaAprovacaoDaProtecao.Conferir(processo, catalogo.TiposVivos, catalogo.Fatos).Errors.Select(ItemDaProtecaoPendente),
            .. ItemDaVersaoIlegivel(publicada.Falha),
        ];
        AvisoConformidadeDto[] avisos = [.. ChecklistDeConformidade.AvaliarAvisos(EstadoDaConfiguracao.De(processo))
            .Select(static aviso => new AvisoConformidadeDto(aviso.Codigo, aviso.Dimensao, aviso.Mensagem))];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens, avisos);
    }

    // A mesma proteção e a mesma aprovação que a publicação e a retificação avaliam, sobre a mesma leitura dos tipos de
    // documento: o painel não aprova o que elas recusam.
    private static IEnumerable<ItemConformidadeDto> ItensDoChecklist(
        ProcessoSeletivo processo,
        ContextoDeContagemDePrazos contexto,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos,
        CatalogoVivo catalogo,
        VersaoPublicada publicada)
    {
        ProtecaoVigenteDasExigencias protecao = ProtecaoVigenteDasExigenciasDoProcesso
            .Montar(processo, catalogo.TiposVivos, catalogo.Fatos, publicada.CongeladoNaVersao).Exigencias;

        return ChecklistDeConformidade
            .AvaliarConformidade(EstadoDaConfiguracao.De(processo), contexto, catalogo.FatosDeModalidade, tiposDeAtoUnicos, protecao)
            .Select(static item => new ItemConformidadeDto(item.Codigo, item.Dimensao, item.Mensagem, item.Ok));
    }

    // O código do item é único no checklist: leva o alvo (o tipo ou o fato), porque vários itens pendentes dividem a
    // mesma invariante.
    private static ItemConformidadeDto ItemDaProtecaoPendente(FieldError erro) =>
        new(
            $"{erro.Error.Code}:{erro.Field}",
            erro.Field?.StartsWith("fatos.", StringComparison.Ordinal) == true ? DimensaoConformidade.ColetaDeFatos : DimensaoConformidade.ExigenciasDocumentais,
            erro.Error.Message,
            Ok: false);

    private static IEnumerable<ItemConformidadeDto> ItemDaVersaoIlegivel(DomainError? falha) =>
        falha is null
            ? []
            : [new ItemConformidadeDto(CodigoDaVersaoPublicadaIlegivel, DimensaoConformidade.Cronograma, falha.Message, Ok: false)];

    /// <summary>O que a versão vigente informa ao checklist: a classificação dos tipos de ato e as exigências que congelou, ou a falha do codec.</summary>
    private sealed record VersaoPublicada(
        IReadOnlyDictionary<string, TipoDeAtoCongelado> TiposCongelados, CongeladoNaVersaoVigente? CongeladoNaVersao, DomainError? Falha);
}
