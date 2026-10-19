namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Handler da <see cref="ObterConformidadeProcessoSeletivoQuery"/>: leitura
/// pura (sem side effects) que mapeia <see cref="ProcessoSeletivo.AvaliarConformidade"/>
/// para o DTO público — não confundir com a conformidade de
/// <c>ObrigatoriedadeLegal</c> (Stories #852/#853), que avalia regras
/// legais configuráveis aplicáveis ao processo.
/// </summary>
/// <remarks>
/// O checklist em si vive em <c>ProcessoSeletivo.AvaliarConformidade()</c> (Domain) — bicondicional
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
/// <b>Avisos.</b> Os avisos vêm de <c>ProcessoSeletivo.AvaliarAvisos()</c> e viajam em lista
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
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ICalendarioVigenteReader calendarioVigenteReader,
        IResolvedorFusoInstitucional resolvedorFuso,
        IFatoCandidatoReader fatoCandidatoReader,
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        IRegistroCodecsEnvelope registroCodecs,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(calendarioVigenteReader);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(tipoAtoPublicadoReader);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return null;
        }

        CalendarioVigenteView? vigente = await calendarioVigenteReader
            .ObterVigenteAsync(cancellationToken)
            .ConfigureAwait(false);

        // A falha de tradução viaja como estado, não como exceção nem como ausência: o
        // preflight precisa reportar exatamente o que a publicação recusaria. Convertê-la em
        // "sem calendário" deixaria o checklist verde para um processo sem contagem sobre dia
        // útil — que de fato não usa o dado — enquanto a publicação recusava por causa dele.
        Result<CalendarioDiasUteisCongelado?> calendario = LeituraDoCalendarioVigente.Traduzir(vigente);

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        var contexto = new ContextoDeContagemDePrazos(
            calendario.IsSuccess ? calendario.Value : null,
            FalhaDoCalendarioVigente: calendario.IsFailure ? calendario.Error : null,
            FusoInstitucional: fuso.IsSuccess ? fuso.Value : null);

        IReadOnlyList<Guid> atosDaLinhagem = await processoSeletivoRepository
            .ObterAtosCriadoresAsync(processo.Id, cancellationToken)
            .ConfigureAwait(false);

        // Como a retificação, o checklist trata o tipo que a versão vigente já congelou pela classificação
        // congelada; só o processo ainda não publicado, ou o tipo novo, lê o catálogo de hoje. Uma versão que
        // o codec não lê é recusada pela própria retificação, e o checklist mostra essa recusa como pendência
        // em vez de avaliar os tipos pelo catálogo de hoje e parecer conforme.
        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(processo.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados = new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal);
        DomainError? falhaDaVersaoPublicada = null;
        if (versaoAtual is not null)
        {
            Result<IReadOnlyDictionary<string, TipoDeAtoCongelado>> congelados =
                CongelamentoDosTiposDeAto.DaVersao(registroCodecs, versaoAtual);
            if (congelados.IsSuccess)
            {
                jaCongelados = congelados.Value!;
            }
            else
            {
                falhaDaVersaoPublicada = congelados.Error;
            }
        }

        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos = await ResolucaoDosTiposDeAtoUnicos
            .ParaChecklistAsync(
                tipoAtoPublicadoReader, atoDoProcessoRepository, processo, atosDaLinhagem,
                ResolucaoDosTiposDeAtoUnicos.Hoje(timeProvider, fuso.IsSuccess ? fuso.Value : null),
                jaCongelados,
                cancellationToken)
            .ConfigureAwait(false);

        FatosDeModalidade fatosDeModalidade = VocabularioDeFatos.ComValoresDeModalidade(
            await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false));

        ItemConformidadeDto[] itens = [.. processo.AvaliarConformidade(contexto, fatosDeModalidade, tiposDeAtoUnicos)
            .Select(static item => new ItemConformidadeDto(item.Codigo, item.Dimensao, item.Mensagem, item.Ok))];
        if (falhaDaVersaoPublicada is not null)
        {
            itens = [.. itens, new ItemConformidadeDto(
                CodigoDaVersaoPublicadaIlegivel,
                DimensaoConformidade.Cronograma,
                falhaDaVersaoPublicada.Message,
                Ok: false)];
        }

        AvisoConformidadeDto[] avisos = [.. processo.AvaliarAvisos()
            .Select(static aviso => new AvisoConformidadeDto(aviso.Codigo, aviso.Dimensao, aviso.Mensagem))];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens, avisos);
    }
}
