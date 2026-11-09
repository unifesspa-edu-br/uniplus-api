namespace Unifesspa.UniPlus.Autorizacao.Apoio;

using System.Reflection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta perfis para os testes pelas operações do domínio, no estado que o teste precisa: com a versão pendente, com a
/// versão em vigor ou de governança. Fonte única, compartilhada por quem precisa de um perfil pronto.
/// </summary>
public static class PerfisDeTeste
{
    public const string Protocolo = "23479.000123/2026-11";

    public const long TamanhoDoDocumento = 2048;

    public static readonly Identidade Autor = new("https://idp.test/realms/unifesspa", "diogo.souza");

    public static readonly DateTimeOffset Instante = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    public static readonly ContextoDaVersao SemContexto = new(NomeEmUsoPorOutroPerfil: false, TemConcessaoNaoRevogada: false);

    public static ConteudoDaVersao Conteudo(
        string nome, string descricao, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo) =>
        Garantir(ConteudoDaVersao.Validar(nome, descricao, permissoes, Protocolo, TamanhoDoDocumento, catalogo));

    /// <summary>Perfil comum com a versão 1 pendente, esperando o envio do documento.</summary>
    public static Perfil Pendente(
        string nome, string descricao, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo) =>
        Garantir(Perfil.Criar(Conteudo(nome, descricao, permissoes, catalogo), Autor, Instante));

    /// <summary>Perfil comum com a versão 1 em vigor: o documento foi enviado e verificado.</summary>
    public static Perfil Vigente(
        string nome, string descricao, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo)
    {
        Perfil perfil = Pendente(nome, descricao, permissoes, catalogo);
        EntrarEmVigor(perfil, catalogo);

        return perfil;
    }

    /// <summary>Cria uma versão nova do perfil, com o mesmo nome, e a leva à vigência.</summary>
    public static void NovaVersaoVigente(
        Perfil perfil, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        Garantir(perfil.CriarVersao(
            Conteudo(perfil.Nome, perfil.Descricao, permissoes, catalogo), SemContexto, Autor, Instante, catalogo));
        EntrarEmVigor(perfil, catalogo);
    }

    /// <summary>Leva a versão pendente do perfil, pelo envio e pela verificação do documento, à vigência.</summary>
    public static DesfechoDoDocumentoVerificado EntrarEmVigor(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = Garantir(perfil.ConfirmarDocumento(versao.Id, "etag-de-teste", Instante));

        return perfil.RegistrarDocumentoVerificado(documento.Id, new string('a', 64), Instante, SemContexto, catalogo);
    }

    /// <summary>
    /// Perfil de governança: as permissões são as próprias do perfil, sem versão, como a carga inicial as grava. O
    /// catálogo informado só precisa conhecer os códigos.
    /// </summary>
    public static Perfil DeGovernanca(
        string nome,
        string descricao,
        IEnumerable<string> permissoes,
        ICatalogoDePermissoesConsultavel catalogo,
        TipoDeGovernanca tipo = TipoDeGovernanca.GestorDeAcessos)
    {
        string[] codigos = [.. permissoes];
        Perfil perfil = Pendente(nome, descricao, codigos, catalogo);
        Atribuir(perfil, nameof(Perfil.Governanca), tipo);
        List<PermissaoDoPerfil> proprias = ListaPrivada<PermissaoDoPerfil>(perfil, "_permissoes");
        proprias.AddRange(codigos.Select(NovaPermissaoDoPerfil));

        return perfil;
    }

    public static T Garantir<T>(Result<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException(string.Join("; ", resultado.Errors.Select(e => e.Error.Code)));
    }

    private static PermissaoDoPerfil NovaPermissaoDoPerfil(string codigo) =>
        (PermissaoDoPerfil)Activator.CreateInstance(
            typeof(PermissaoDoPerfil), BindingFlags.Instance | BindingFlags.NonPublic, null, [codigo], null)!;

    private static List<T> ListaPrivada<T>(Perfil perfil, string campo) =>
        (List<T>)typeof(Perfil).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(perfil)!;

    private static void Atribuir(Perfil perfil, string propriedade, object valor) =>
        typeof(Perfil).GetProperty(propriedade)!.SetValue(perfil, valor);
}
