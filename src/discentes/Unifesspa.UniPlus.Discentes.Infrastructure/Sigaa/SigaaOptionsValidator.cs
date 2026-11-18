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

    private static readonly TimeSpan UmSegundo = TimeSpan.FromSeconds(1);

    private static readonly string PaginaForaDoLimiteDaOrigem =
        $"Sigaa:Paginacao:ItensPorPagina precisa estar entre 1 e {PaginacaoDoSigaa.MaximoItensPorPagina}, "
        + "que é o limite imposto pela origem.";

    /// <summary>
    /// A biblioteca de resiliência também recusa espera negativa, mas só ao montar a
    /// política — o que acontece na primeira consulta, não na subida. Recusar aqui põe a
    /// falha junto das outras de configuração, antes de o processo se declarar pronto.
    /// </summary>
    private const string EsperaNegativaEntreTentativas =
        "Sigaa:Resiliencia:EsperaBaseEntreTentativas não pode ser negativa.";

    private const string TimeoutTotalMenorQueODaTentativa =
        "Sigaa:Resiliencia:TimeoutTotal não pode ser menor que o limite de uma tentativa "
        + "isolada — a operação inteira precisa comportar ao menos a primeira delas.";

    /// <summary>
    /// A margem precisa caber nas duas validades, não só no limite. Se ela alcançar a
    /// validade assumida, todo token obtido quando a expiração não é legível já nasce
    /// vencido, e cada chamada ao SIGAA passa a esperar por uma autenticação nova — uma
    /// por chamada, em fila.
    /// </summary>
    private const string MargemNaoCabeNaValidadeMaxima =
        "Sigaa:Token:MargemDeRenovacao precisa ser menor que a validade máxima "
        + "do token — do contrário todo token nasceria vencido e cada requisição renovaria.";

    private const string MargemNaoCabeNaValidadeAssumida =
        "Sigaa:Token:MargemDeRenovacao precisa ser menor que "
        + "Sigaa:Token:ValidadeAssumida — quando a expiração do token não é "
        + "legível, vale a validade assumida, e uma margem que a consome inteira faz "
        + "todo token nascer vencido.";

    /// <summary>
    /// A biblioteca de resiliência recusa essa combinação, mas só quando a primeira
    /// requisição acontece. Aqui a recusa sai na subida do processo, junto com as outras,
    /// e diz o que ajustar.
    /// </summary>
    private const string JanelaMenorQueODobroDaTentativa =
        "Sigaa:Resiliencia:JanelaDeAmostragemDaInterrupcao precisa ser pelo menos o dobro de "
        + "Sigaa:Resiliencia:TimeoutPorTentativa — numa janela que mal comporta uma "
        + "tentativa não se forma amostra, e a interrupção por falhas nunca reagiria.";

    private static readonly (Func<ResilienciaDoSigaa, bool> Invalida, string Mensagem)[] RegrasDaInterrupcao =
    [
        (r => r.ChamadasMinimasParaAvaliarInterrupcao < 1,
            "Sigaa:Resiliencia:ChamadasMinimasParaAvaliarInterrupcao precisa ser pelo menos 1."),
        (r => r.JanelaDeAmostragemDaInterrupcao < UmSegundo,
            "Sigaa:Resiliencia:JanelaDeAmostragemDaInterrupcao precisa ser pelo menos 1 segundo."),
        (r => r.JanelaDeAmostragemDaInterrupcao >= UmSegundo
            && r.JanelaDeAmostragemDaInterrupcao < r.TimeoutPorTentativa * 2,
            JanelaMenorQueODobroDaTentativa),
        (r => r.ProporcaoDeFalhaParaInterromper is <= 0 or > 1,
            "Sigaa:Resiliencia:ProporcaoDeFalhaParaInterromper precisa estar entre 0 (exclusivo) e 1."),
        (r => r.DuracaoDaInterrupcao < UmSegundo,
            "Sigaa:Resiliencia:DuracaoDaInterrupcao precisa ser pelo menos 1 segundo."),
    ];

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
        FalhasDaInterrupcao,
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
        if (string.IsNullOrWhiteSpace(options.Credenciais.Usuario))
        {
            yield return "Sigaa:Usuario é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(options.Credenciais.Senha))
        {
            yield return SenhaAusente;
        }
    }

    private static IEnumerable<string> FalhasDaPaginacao(SigaaOptions options)
    {
        if (options.Paginacao.ItensPorPagina is < 1 or > PaginacaoDoSigaa.MaximoItensPorPagina)
        {
            yield return PaginaForaDoLimiteDaOrigem;
        }

        if (options.Paginacao.GrauDeParalelismo < 1)
        {
            yield return "Sigaa:Paginacao:GrauDeParalelismo precisa ser pelo menos 1.";
        }
    }

    private static IEnumerable<string> FalhasDasRetentativas(SigaaOptions options)
    {
        if (options.Resiliencia.MaximoDeRetentativas < 0)
        {
            yield return "Sigaa:Resiliencia:MaximoDeRetentativas não pode ser negativo.";
        }

        if (options.Resiliencia.EsperaBaseEntreTentativas < TimeSpan.Zero)
        {
            yield return EsperaNegativaEntreTentativas;
        }
    }

    private static IEnumerable<string> FalhasDosTempos(SigaaOptions options)
    {
        if (options.Resiliencia.TimeoutPorTentativa < UmSegundo)
        {
            yield return "Sigaa:Resiliencia:TimeoutPorTentativa precisa ser pelo menos 1 segundo.";
        }

        if (options.Resiliencia.TimeoutTotal < options.Resiliencia.TimeoutPorTentativa)
        {
            yield return TimeoutTotalMenorQueODaTentativa;
        }
    }

    private static IEnumerable<string> FalhasDosLimitesDoToken(SigaaOptions options)
    {
        if (options.Token.MargemDeRenovacao < TimeSpan.Zero)
        {
            yield return "Sigaa:Token:MargemDeRenovacao não pode ser negativa.";
        }

        if (options.Token.ValidadeAssumida < UmSegundo)
        {
            yield return "Sigaa:Token:ValidadeAssumida precisa ser pelo menos 1 segundo.";
        }

        if (options.Token.ValidadeMaxima < UmSegundo)
        {
            yield return "Sigaa:Token:ValidadeMaxima precisa ser pelo menos 1 segundo.";
        }
    }

    private static IEnumerable<string> FalhasDaMargemDeRenovacao(SigaaOptions options)
    {
        if (options.Token.MargemDeRenovacao >= options.Token.ValidadeMaxima)
        {
            yield return MargemNaoCabeNaValidadeMaxima;
        }

        if (options.Token.MargemDeRenovacao >= options.Token.ValidadeAssumida)
        {
            yield return MargemNaoCabeNaValidadeAssumida;
        }
    }

    private static IEnumerable<string> FalhasDaInterrupcao(SigaaOptions options)
    {
        foreach ((Func<ResilienciaDoSigaa, bool> invalida, string mensagem) in RegrasDaInterrupcao)
        {
            if (invalida(options.Resiliencia))
            {
                yield return mensagem;
            }
        }
    }
}
