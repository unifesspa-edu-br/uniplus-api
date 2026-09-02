namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

/// <summary>
/// Recusa configuração de sincronização que só falharia de madrugada.
/// </summary>
internal sealed class SincronizacaoOptionsValidator : IValidateOptions<SincronizacaoOptions>
{
    public ValidateOptionsResult Validate(string? name, SincronizacaoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas = [];

        if (string.IsNullOrWhiteSpace(options.NivelDeEnsino))
        {
            falhas.Add("Discentes:Sincronizacao:NivelDeEnsino é obrigatório.");
        }

        if (options.AnosDeIngressoConsiderados < 1)
        {
            falhas.Add("Discentes:Sincronizacao:AnosDeIngressoConsiderados precisa ser pelo menos 1.");
        }

        if (options.SituacoesEmAndamento.Count == 0)
        {
            falhas.Add(
                "Discentes:Sincronizacao:SituacoesEmAndamento não pode ser vazio — sem ele, "
                + "vínculos em andamento com ingresso antigo deixariam de ser alcançados.");
        }

        // Lote não positivo não falha de imediato: um valor negativo derruba a execução
        // depois de a marca dela já ter sido criada, e zero faz cada vínculo virar uma
        // transação, degradando a sincronização diária sem nada acusar.
        if (options.TamanhoDoLote < 1)
        {
            falhas.Add("Discentes:Sincronizacao:TamanhoDoLote precisa ser pelo menos 1.");
        }

        return falhas.Count > 0
            ? ValidateOptionsResult.Fail(falhas)
            : ValidateOptionsResult.Success;
    }
}
