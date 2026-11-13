namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using System.Collections.Frozen;
using System.Globalization;
using System.Net;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// O texto de cada aviso: assunto e abertura fixos por tipo, e os campos do modelo no corpo, em texto e em HTML. Os
/// valores do cadastro vão escapados no HTML. O tipo sem texto é erro de programação.
/// </summary>
internal static class RedacaoDoAviso
{
    private static readonly FrozenDictionary<TipoDeAviso, TextoDoAviso> Textos = TextosDosAvisosDaVersao.Todos
        .Concat(TextosDaJanela.Todos)
        .Concat(TextosDosAvisosDaContencao.Todos)
        .Concat(TextosDosAvisosDeReclassificacaoEDivergencia.Todos)
        .Concat(TextosDoAvisoDeTermoExigido.Todos)
        .ToFrozenDictionary();

    public static string Assunto(TipoDeAviso tipo) => TextoDe(tipo).Assunto;

    public static string CorpoEmTexto(ModeloDeAviso modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        string campos = string.Concat(Campos(modelo).Select(static c => $"\n{c.Rotulo}: {c.Valor}"));

        return $"{Abertura(modelo)}\n{campos}\n\nAbra em: {modelo.Link}";
    }

    public static string CorpoEmHtml(ModeloDeAviso modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        string campos = string.Join("<br>", Campos(modelo).Select(static c => $"{c.Rotulo}: <b>{WebUtility.HtmlEncode(c.Valor)}</b>"));

        return $"<p>{Abertura(modelo)}</p><p>{campos}</p><p><a href=\"{WebUtility.HtmlEncode(modelo.Link)}\">Abrir</a></p>";
    }

    private static TextoDoAviso TextoDe(TipoDeAviso tipo) =>
        Textos.TryGetValue(tipo, out TextoDoAviso? texto) ? texto : throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null);

    private static string Abertura(ModeloDeAviso modelo) =>
        modelo.Forma is { } forma && TextosDaJanela.PorForma.TryGetValue((modelo.Tipo, forma), out string? daForma)
            ? daForma
            : TextoDe(modelo.Tipo).Abertura;

    private static IEnumerable<(string Rotulo, string Valor)> Campos(ModeloDeAviso modelo)
    {
        yield return ("Perfil", modelo.PerfilNome);

        foreach ((string rotulo, string? valor) in Opcionais(modelo).Concat(ContagensDe(modelo.Contagens)))
        {
            if (valor is not null)
            {
                yield return (rotulo, valor);
            }
        }
    }

    private static IEnumerable<(string Rotulo, string? Valor)> Opcionais(ModeloDeAviso modelo)
    {
        yield return ("Versão", modelo.VersaoNumero?.ToString(CultureInfo.InvariantCulture));
        yield return ("Etapa", modelo.Etapa is { } etapa ? NomeDaEtapa(etapa) : null);
        yield return ("Permissões", modelo.Permissoes.Count > 0 ? string.Join(", ", modelo.Permissoes) : null);
        yield return ("Prazo", modelo.Prazo is { } prazo ? $"{prazo.ParaHorarioBrasilia().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} (Brasília)" : null);
        yield return ("Horas restantes", modelo.HorasRestantes?.ToString(CultureInfo.InvariantCulture));
        yield return ("Mudança", modelo.Mudanca);
    }

    private static IEnumerable<(string Rotulo, string? Valor)> ContagensDe(ContagensDoAviso? contagens)
    {
        yield return ("Concessões pendentes", Numero(contagens?.Pendentes));
        yield return ("Aguardando o Representante do controlador", Numero(contagens?.Aguardando));
        yield return ("Migradas", Numero(contagens?.Migradas));
        yield return ("Revogadas", Numero(contagens?.Revogadas));
    }

    private static string? Numero(int? valor) => valor?.ToString(CultureInfo.InvariantCulture);

    private static string NomeDaEtapa(EtapaDaVersao etapa) => etapa == EtapaDaVersao.GestaoDeAcessos ? "gestão de acessos" : "Representante do controlador";
}
