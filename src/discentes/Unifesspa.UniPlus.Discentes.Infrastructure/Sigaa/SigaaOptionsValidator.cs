namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

/// <summary>
/// Recusa configuração do SIGAA que não permitiria sincronizar, na subida do processo —
/// antes que a primeira execução diária falhe de madrugada por endereço vazio ou
/// credencial ausente.
/// </summary>
internal sealed class SigaaOptionsValidator : IValidateOptions<SigaaOptions>
{
    public ValidateOptionsResult Validate(string? name, SigaaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas = [];

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            falhas.Add("Sigaa:BaseUrl é obrigatório.");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? endereco))
        {
            falhas.Add("Sigaa:BaseUrl precisa ser um endereço absoluto.");
        }
        else if (endereco.Scheme != Uri.UriSchemeHttps)
        {
            // Sem canal cifrado, a senha do usuário de serviço, o token e o CPF de cada
            // vínculo trafegam em texto claro. Nenhum ambiente justifica isso: o que se
            // perderia num teste local é menos do que o risco de a exceção virar hábito.
            falhas.Add(
                "Sigaa:BaseUrl precisa usar https. A integração transporta credencial de "
                + "serviço, token de acesso e CPF; em canal não cifrado, tudo isso vai "
                + "legível na rede.");
        }

        if (string.IsNullOrWhiteSpace(options.Usuario))
        {
            falhas.Add("Sigaa:Usuario é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(options.Senha))
        {
            falhas.Add(
                "Sigaa:Senha é obrigatório e não tem valor padrão. Em execução real vem da "
                + "variável de ambiente Sigaa__Senha, populada a partir do cofre; em máquina de "
                + "desenvolvimento, de user-secrets. Nunca de arquivo versionado.");
        }

        if (options.ItensPorPagina is < 1 or > SigaaOptions.MaximoItensPorPagina)
        {
            falhas.Add(
                $"Sigaa:ItensPorPagina precisa estar entre 1 e {SigaaOptions.MaximoItensPorPagina}, "
                + "que é o teto imposto pela origem.");
        }

        if (options.GrauDeParalelismo < 1)
        {
            falhas.Add("Sigaa:GrauDeParalelismo precisa ser pelo menos 1.");
        }

        if (options.MaximoDeRetentativas < 0)
        {
            falhas.Add("Sigaa:MaximoDeRetentativas não pode ser negativo.");
        }

        // A biblioteca de resiliência também recusa espera negativa, mas só ao montar a
        // política — o que acontece na primeira consulta, não na subida. Recusar aqui põe
        // a falha junto das outras de configuração, antes de o processo se declarar pronto.
        if (options.EsperaBaseEntreTentativasEmMilissegundos < 0)
        {
            falhas.Add("Sigaa:EsperaBaseEntreTentativasEmMilissegundos não pode ser negativa.");
        }

        if (options.TimeoutPorTentativaEmSegundos < 1)
        {
            falhas.Add("Sigaa:TimeoutPorTentativaEmSegundos precisa ser pelo menos 1.");
        }

        if (options.TimeoutTotalEmSegundos < options.TimeoutPorTentativaEmSegundos)
        {
            falhas.Add(
                "Sigaa:TimeoutTotalEmSegundos não pode ser menor que o limite de uma tentativa "
                + "isolada — a operação inteira precisa comportar ao menos a primeira delas.");
        }

        if (options.MargemDeRenovacaoDoTokenEmSegundos < 0)
        {
            falhas.Add("Sigaa:MargemDeRenovacaoDoTokenEmSegundos não pode ser negativa.");
        }

        if (options.ValidadeAssumidaDoTokenEmSegundos < 1)
        {
            falhas.Add("Sigaa:ValidadeAssumidaDoTokenEmSegundos precisa ser pelo menos 1.");
        }

        if (options.ValidadeMaximaDoTokenEmSegundos < 1)
        {
            falhas.Add("Sigaa:ValidadeMaximaDoTokenEmSegundos precisa ser pelo menos 1.");
        }

        // A margem precisa caber nas duas validades, não só no teto. Se ela alcançar a
        // validade assumida, todo token obtido quando a expiração não é legível já nasce
        // vencido, e cada chamada ao SIGAA passa a esperar por uma autenticação nova —
        // uma por chamada, em fila.
        if (options.MargemDeRenovacaoDoTokenEmSegundos >= options.ValidadeMaximaDoTokenEmSegundos)
        {
            falhas.Add(
                "Sigaa:MargemDeRenovacaoDoTokenEmSegundos precisa ser menor que a validade máxima "
                + "do token — do contrário todo token nasceria vencido e cada requisição renovaria.");
        }

        if (options.MargemDeRenovacaoDoTokenEmSegundos >= options.ValidadeAssumidaDoTokenEmSegundos)
        {
            falhas.Add(
                "Sigaa:MargemDeRenovacaoDoTokenEmSegundos precisa ser menor que "
                + "Sigaa:ValidadeAssumidaDoTokenEmSegundos — quando a expiração do token não é "
                + "legível, vale a validade assumida, e uma margem que a consome inteira faz "
                + "todo token nascer vencido.");
        }

        if (options.ChamadasMinimasParaAvaliarCorte < 1)
        {
            falhas.Add("Sigaa:ChamadasMinimasParaAvaliarCorte precisa ser pelo menos 1.");
        }

        if (options.JanelaDeAmostragemDoCorteEmSegundos < 1)
        {
            falhas.Add("Sigaa:JanelaDeAmostragemDoCorteEmSegundos precisa ser pelo menos 1.");
        }
        else if (options.JanelaDeAmostragemDoCorteEmSegundos < options.TimeoutPorTentativaEmSegundos * 2)
        {
            // A biblioteca de resiliência recusa essa combinação, mas só quando a primeira
            // requisição acontece. Aqui a recusa sai na subida do processo, junto com as
            // outras, e diz o que ajustar.
            falhas.Add(
                "Sigaa:JanelaDeAmostragemDoCorteEmSegundos precisa ser pelo menos o dobro de "
                + "Sigaa:TimeoutPorTentativaEmSegundos — numa janela que mal comporta uma "
                + "tentativa não se forma amostra, e o corte de circuito nunca reagiria.");
        }

        if (options.ProporcaoDeFalhaParaAbrirCorte is <= 0 or > 1)
        {
            falhas.Add("Sigaa:ProporcaoDeFalhaParaAbrirCorte precisa estar entre 0 (exclusivo) e 1.");
        }

        if (options.DuracaoDoCorteEmSegundos < 1)
        {
            falhas.Add("Sigaa:DuracaoDoCorteEmSegundos precisa ser pelo menos 1.");
        }

        return falhas.Count > 0
            ? ValidateOptionsResult.Fail(falhas)
            : ValidateOptionsResult.Success;
    }
}
