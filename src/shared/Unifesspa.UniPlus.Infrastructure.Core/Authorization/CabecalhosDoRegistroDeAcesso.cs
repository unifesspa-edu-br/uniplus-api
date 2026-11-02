namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Diagnostics;
using System.Globalization;
using System.Text;

using Confluent.Kafka;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Os cabeçalhos da mensagem de um lote do registro de acesso: o que o consumidor precisa para escolher o leitor, agrupar
/// a requisição e separar o sinal de segurança sem abrir o conteúdo. Nunca levam dado do ator nem do resource, porque
/// as ferramentas de inspeção do broker os exibem sem a proteção do conteúdo.
/// </summary>
internal static class CabecalhosDoRegistroDeAcesso
{
    internal const string ContentType = "content-type";
    internal const string Tipo = "uniplus.tipo";
    internal const string VersaoSchema = "uniplus.versao-schema";
    internal const string ExecucaoId = "uniplus.execucao-id";
    internal const string ProduzidoEm = "uniplus.produzido-em";
    internal const string ClienteOidc = "uniplus.cliente-oidc";
    internal const string Quantidade = "uniplus.quantidade";
    internal const string Resultado = "uniplus.resultado";
    internal const string SensibilidadeMaxima = "uniplus.sensibilidade-maxima";
    internal const string Traceparent = "traceparent";

    internal const string TipoDoLote = "registro-de-acesso.lote";
    internal const string VersaoDoSchema = "1";

    private static readonly Dictionary<string, int> OrdemDaSensibilidade = Enum.GetValues<ClassificacaoDeProtecao>()
        .Where(static c => c != ClassificacaoDeProtecao.Nenhuma)
        .ToDictionary(static c => ValoresCanonicos.De(c), static c => (int)c, StringComparer.Ordinal);

    public static Headers De(IReadOnlyList<RegistroDecisaoAcesso> registros, DateTimeOffset produzidoEm, Activity? atividade)
    {
        Headers cabecalhos = new()
        {
            { ContentType, Utf8("application/json") },
            { Tipo, Utf8(TipoDoLote) },
            { VersaoSchema, Utf8(VersaoDoSchema) },
            { ExecucaoId, Utf8(registros[0].ExecucaoId.ToString()) },
            { ProduzidoEm, Utf8(produzidoEm.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)) },
            { Quantidade, Utf8(registros.Count.ToString(CultureInfo.InvariantCulture)) },
            { Resultado, Utf8(ResultadoDoLote(registros)) },
            { SensibilidadeMaxima, Utf8(registros.Select(static r => r.Sensibilidade).MaxBy(static s => OrdemDaSensibilidade[s])!) },
        };

        if (registros[0].ClienteOidc is { } cliente)
        {
            cabecalhos.Add(ClienteOidc, Utf8(cliente));
        }

        if (atividade is { IdFormat: ActivityIdFormat.W3C, Id: { } traceparent })
        {
            cabecalhos.Add(Traceparent, Utf8(traceparent));
        }

        return cabecalhos;
    }

    private static string ResultadoDoLote(IReadOnlyList<RegistroDecisaoAcesso> registros)
    {
        string[] resultados = [.. registros.Select(ResultadoDe).Distinct(StringComparer.Ordinal)];

        return resultados.Length == 1 ? resultados[0] : "misto";
    }

    private static string ResultadoDe(RegistroDecisaoAcesso registro) => registro.Resultado switch
    {
        ResultadoDoAcesso.Permitido => "permitido",
        ResultadoDoAcesso.Impedido => "impedido",
        _ => "negado",
    };

    private static byte[] Utf8(string valor) => Encoding.UTF8.GetBytes(valor);
}
