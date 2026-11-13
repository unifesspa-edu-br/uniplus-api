namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Textos e versões do termo de responsabilidade para os testes; o registro da vigência nasce como a carga inicial o grava.</summary>
internal static class TermosDeTeste
{
    public const string Ato = "Portaria nº 123/2026 da Reitoria";

    public static readonly DateTimeOffset PublicadoEm = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    public static readonly Identidade Autor = new("https://idp.test/realms/unifesspa", "diogo.souza", "Diogo Bruno Filipe Souza");

    /// <summary>Um texto válido, que a marca torna distinto de outro.</summary>
    public static string Texto(string marca = "primeira versão") =>
        $"Termo de responsabilidade ({marca}). Declaro que usarei o acesso concedido somente para as finalidades da minha função, "
        + "que guardarei sigilo sobre os dados pessoais a que tiver acesso e que comunicarei à unidade responsável qualquer incidente de segurança.";

    /// <summary>O registro da vigência sem versão publicada, como a carga inicial o deixa.</summary>
    public static TermoDeResponsabilidade RegistroSemVersao() =>
        (TermoDeResponsabilidade)Activator.CreateInstance(typeof(TermoDeResponsabilidade), nonPublic: true)!;

    /// <summary>A primeira versão publicada de um registro novo.</summary>
    public static VersaoDoTermo Publicada(string marca = "primeira versão") =>
        RegistroSemVersao().Publicar(Conteudo(marca), Autor, null, null, PublicadoEm).Value!;

    public static ConteudoDoTermo Conteudo(string marca = "primeira versão") => ConteudoDoTermo.Criar(Texto(marca), Ato).Value!;
}
