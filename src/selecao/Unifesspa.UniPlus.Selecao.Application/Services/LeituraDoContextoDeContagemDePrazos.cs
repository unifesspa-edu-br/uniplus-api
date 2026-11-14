namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O contexto que o agregado não tem para contar prazos: o calendário vigente é do módulo Configuração, e o
/// reconhecimento do fuso institucional depende da base de fusos do runtime. As duas leituras chegam prontas ao domínio,
/// que não lê reader nem serviço (ADR-0042).
/// </summary>
/// <remarks>
/// A falha de tradução do calendário e a do fuso viajam como estado, não como exceção nem como ausência: o preflight
/// precisa reportar exatamente o que a publicação recusaria. Convertê-las em "sem calendário" ou "sem fuso" deixaria o
/// checklist verde para um processo sem contagem sobre dia útil, que de fato não usa o dado, enquanto a publicação
/// recusava por causa dele. O calendário é lido uma só vez por operação: a mesma resposta alimenta o portão da raiz e o
/// bloco congelado do envelope, e ler duas vezes abriria a janela em que o dataset muda entre validar e congelar.
/// </remarks>
public sealed class LeituraDoContextoDeContagemDePrazos(ICalendarioVigenteReader calendarios, IResolvedorFusoInstitucional resolvedorFuso)
{
    internal async Task<ContextoDeContagemDePrazos> LerAsync(CancellationToken cancellationToken)
    {
        CalendarioVigenteView? vigente = await calendarios.ObterVigenteAsync(cancellationToken).ConfigureAwait(false);
        Result<CalendarioDiasUteisCongelado?> calendario = LeituraDoCalendarioVigente.Traduzir(vigente);
        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();

        return new ContextoDeContagemDePrazos(
            calendario.IsSuccess ? calendario.Value : null,
            FalhaDoCalendarioVigente: calendario.IsFailure ? calendario.Error : null,
            FusoInstitucional: fuso.IsSuccess ? fuso.Value : null);
    }
}
