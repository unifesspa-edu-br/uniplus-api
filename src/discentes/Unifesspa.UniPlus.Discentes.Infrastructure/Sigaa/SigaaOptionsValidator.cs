namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Options;

/// <summary>
/// Recusa configuração do SIGAA que não permitiria sincronizar, na subida do processo —
/// antes que a primeira execução diária falhe de madrugada por endereço vazio ou
/// credencial ausente.
/// </summary>
internal sealed class SigaaOptionsValidator : IValidateOptions<SigaaOptions>
{
    /// <summary>
    /// Sem canal cifrado, a senha do usuário de serviço, o token e o CPF de cada vínculo
    /// trafegam em texto claro. Nenhum ambiente justifica isso: o que se perderia num teste
    /// local é menos do que o risco de a exceção virar hábito.
    /// </summary>
    private const string EnderecoSemCanalCifrado =
        "Sigaa:BaseUrl precisa usar https. A integração transporta credencial de "
        + "serviço, token de acesso e CPF; em canal não cifrado, tudo isso vai "
        + "legível na rede.";

    private const string SenhaAusente =
        "Sigaa:Senha é obrigatório e não tem valor padrão. Em execução real vem da "
        + "variável de ambiente Sigaa__Senha, populada a partir do cofre; em máquina de "
        + "desenvolvimento, de user-secrets. Nunca de arquivo versionado.";

    private static readonly string PaginaForaDoLimiteDaOrigem =
        $"Sigaa:ItensPorPagina precisa estar entre 1 e {SigaaOptions.MaximoItensPorPagina}, "
        + "que é o limite imposto pela origem.";

    /// <summary>
    /// A biblioteca de resiliência também recusa espera negativa, mas só ao montar a
    /// política — o que acontece na primeira consulta, não na subida. Recusar aqui põe a
    /// falha junto das outras de configuração, antes de o processo se declarar pronto.
    /// </summary>
    private const string EsperaNegativaEntreTentativas =
        "Sigaa:EsperaBaseEntreTentativasEmMilissegundos não pode ser negativa.";

    private const string TimeoutTotalMenorQueODaTentativa =
        "Sigaa:TimeoutTotalEmSegundos não pode ser menor que o limite de uma tentativa "
        + "isolada — a operação inteira precisa comportar ao menos a primeira delas.";

    /// <summary>
    /// A margem precisa caber nas duas validades, não só no limite. Se ela alcançar a
    /// validade assumida, todo token obtido quando a expiração não é legível já nasce
    /// vencido, e cada chamada ao SIGAA passa a esperar por uma autenticação nova — uma
    /// por chamada, em fila.
    /// </summary>
    private const string MargemNaoCabeNaValidadeMaxima =
        "Sigaa:MargemDeRenovacaoDoTokenEmSegundos precisa ser menor que a validade máxima "
        + "do token — do contrário todo token nasceria vencido e cada requisição renovaria.";

    private const string MargemNaoCabeNaValidadeAssumida =
        "Sigaa:MargemDeRenovacaoDoTokenEmSegundos precisa ser menor que "
        + "Sigaa:ValidadeAssumidaDoTokenEmSegundos — quando a expiração do token não é "
        + "legível, vale a validade assumida, e uma margem que a consome inteira faz "
        + "todo token nascer vencido.";

    /// <summary>
    /// A biblioteca de resiliência recusa essa combinação, mas só quando a primeira
    /// requisição acontece. Aqui a recusa sai na subida do processo, junto com as outras,
    /// e diz o que ajustar.
    /// </summary>
    private const string JanelaMenorQueODobroDaTentativa =
        "Sigaa:JanelaDeAmostragemDaInterrupcaoEmSegundos precisa ser pelo menos o dobro de "
        + "Sigaa:TimeoutPorTentativaEmSegundos — numa janela que mal comporta uma "
        + "tentativa não se forma amostra, e a interrupção por falhas nunca reagiria.";

    /// <summary>
    /// As conferências, na ordem em que as falhas aparecem na mensagem.
    /// </summary>
    private static readonly Func<SigaaOptions, IEnumerable<string>>[] Conferencias =
    [
        FalhasDoEndereco,
        FalhasDaCredencial,
        FalhasDaPaginacao,
        FalhasDasRetentativas,
        FalhasDosTempos,
        FalhasDosLimitesDoToken,
        FalhasDaMargemDeRenovacao,
        FalhasDaAmostragemDaInterrupcao,
        FalhasDoDisparoDaInterrupcao,
    ];

    public ValidateOptionsResult Validate(string? name, SigaaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string[] falhas = [.. Conferencias.SelectMany(conferir => conferir(options))];

        return falhas.Length > 0 ? ValidateOptionsResult.Fail(falhas) : ValidateOptionsResult.Success;
    }

    private static IEnumerable<string> FalhasDoEndereco(SigaaOptions options)
    {
        if (options.BaseUrl is null)
        {
            yield return "Sigaa:BaseUrl é obrigatório.";
        }
        else if (!options.BaseUrl.IsAbsoluteUri)
        {
            yield return "Sigaa:BaseUrl precisa ser um endereço absoluto.";
        }
        else if (options.BaseUrl.Scheme != Uri.UriSchemeHttps)
        {
            yield return EnderecoSemCanalCifrado;
        }
    }

    private static IEnumerable<string> FalhasDaCredencial(SigaaOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Usuario))
        {
            yield return "Sigaa:Usuario é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(options.Senha))
        {
            yield return SenhaAusente;
        }
    }

    private static IEnumerable<string> FalhasDaPaginacao(SigaaOptions options)
    {
        if (options.ItensPorPagina is < 1 or > SigaaOptions.MaximoItensPorPagina)
        {
            yield return PaginaForaDoLimiteDaOrigem;
        }

        if (options.GrauDeParalelismo < 1)
        {
            yield return "Sigaa:GrauDeParalelismo precisa ser pelo menos 1.";
        }
    }

    private static IEnumerable<string> FalhasDasRetentativas(SigaaOptions options)
    {
        if (options.MaximoDeRetentativas < 0)
        {
            yield return "Sigaa:MaximoDeRetentativas não pode ser negativo.";
        }

        if (options.EsperaBaseEntreTentativasEmMilissegundos < 0)
        {
            yield return EsperaNegativaEntreTentativas;
        }
    }

    private static IEnumerable<string> FalhasDosTempos(SigaaOptions options)
    {
        if (options.TimeoutPorTentativaEmSegundos < 1)
        {
            yield return "Sigaa:TimeoutPorTentativaEmSegundos precisa ser pelo menos 1.";
        }

        if (options.TimeoutTotalEmSegundos < options.TimeoutPorTentativaEmSegundos)
        {
            yield return TimeoutTotalMenorQueODaTentativa;
        }
    }

    private static IEnumerable<string> FalhasDosLimitesDoToken(SigaaOptions options)
    {
        if (options.MargemDeRenovacaoDoTokenEmSegundos < 0)
        {
            yield return "Sigaa:MargemDeRenovacaoDoTokenEmSegundos não pode ser negativa.";
        }

        if (options.ValidadeAssumidaDoTokenEmSegundos < 1)
        {
            yield return "Sigaa:ValidadeAssumidaDoTokenEmSegundos precisa ser pelo menos 1.";
        }

        if (options.ValidadeMaximaDoTokenEmSegundos < 1)
        {
            yield return "Sigaa:ValidadeMaximaDoTokenEmSegundos precisa ser pelo menos 1.";
        }
    }

    private static IEnumerable<string> FalhasDaMargemDeRenovacao(SigaaOptions options)
    {
        if (options.MargemDeRenovacaoDoTokenEmSegundos >= options.ValidadeMaximaDoTokenEmSegundos)
        {
            yield return MargemNaoCabeNaValidadeMaxima;
        }

        if (options.MargemDeRenovacaoDoTokenEmSegundos >= options.ValidadeAssumidaDoTokenEmSegundos)
        {
            yield return MargemNaoCabeNaValidadeAssumida;
        }
    }

    private static IEnumerable<string> FalhasDaAmostragemDaInterrupcao(SigaaOptions options)
    {
        if (options.ChamadasMinimasParaAvaliarInterrupcao < 1)
        {
            yield return "Sigaa:ChamadasMinimasParaAvaliarInterrupcao precisa ser pelo menos 1.";
        }

        if (options.JanelaDeAmostragemDaInterrupcaoEmSegundos < 1)
        {
            yield return "Sigaa:JanelaDeAmostragemDaInterrupcaoEmSegundos precisa ser pelo menos 1.";
        }
        else if (options.JanelaDeAmostragemDaInterrupcaoEmSegundos < options.TimeoutPorTentativaEmSegundos * 2)
        {
            yield return JanelaMenorQueODobroDaTentativa;
        }
    }

    private static IEnumerable<string> FalhasDoDisparoDaInterrupcao(SigaaOptions options)
    {
        if (options.ProporcaoDeFalhaParaInterromper is <= 0 or > 1)
        {
            yield return "Sigaa:ProporcaoDeFalhaParaInterromper precisa estar entre 0 (exclusivo) e 1.";
        }

        if (options.DuracaoDaInterrupcaoEmSegundos < 1)
        {
            yield return "Sigaa:DuracaoDaInterrupcaoEmSegundos precisa ser pelo menos 1.";
        }
    }
}
