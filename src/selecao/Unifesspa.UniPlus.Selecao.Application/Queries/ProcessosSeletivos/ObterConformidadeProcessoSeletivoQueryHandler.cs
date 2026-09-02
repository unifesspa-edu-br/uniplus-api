namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

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
/// </remarks>
public static class ObterConformidadeProcessoSeletivoQueryHandler
{
    public static async Task<ConformidadeProcessoSeletivoDto?> Handle(
        ObterConformidadeProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICalendarioVigenteReader calendarioVigenteReader,
        IResolvedorFusoInstitucional resolvedorFuso,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(calendarioVigenteReader);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);

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
            FusoInstitucionalReconhecido: fuso.IsSuccess,
            FalhaDoCalendarioVigente: calendario.IsFailure ? calendario.Error : null,
            FusoInstitucional: fuso.IsSuccess ? fuso.Value : null);

        ItemConformidadeDto[] itens = [.. processo.AvaliarConformidade(contexto)
            .Select(static item => new ItemConformidadeDto(item.Codigo, item.Dimensao, item.Mensagem, item.Ok))];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens);
    }
}
