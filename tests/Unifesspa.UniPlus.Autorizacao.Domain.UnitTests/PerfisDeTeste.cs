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

    public static readonly Identidade Autor = new("https://idp.test/realms/unifesspa", "diogo.souza", "Diogo Souza");

    public static readonly Identidade Gestora = new("https://idp.test/realms/unifesspa", "betina.neves", "Betina Neves");

    public static readonly Identidade ProtecaoDeDados = new("https://idp.test/realms/unifesspa", "marlene.campos", "Marlene Campos");

    public static readonly AutoriaDaVersao AutoriaDoAutor = Autoria(Autor);

    public static readonly AutoriaDaVersao AutoriaDaGestora = Autoria(Gestora);

    public static readonly AutoriaDaVersao AutoriaDaProtecaoDeDados = Autoria(ProtecaoDeDados);

    public static readonly DateTimeOffset Instante = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    public static readonly ContextoDaVersao SemContexto = new(NomeEmUsoPorOutroPerfil: false, JanelaDeMigracaoAberta: false);

    public static AutoriaDaVersao Autoria(Identidade pessoa, bool temConcessao = false)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return new AutoriaDaVersao(pessoa, $"{pessoa.Sub}@unifesspa.edu.br", temConcessao);
    }

    public static ConteudoDaVersao Conteudo(
        string nome, string descricao, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo) =>
        Garantir(ConteudoDaVersao.Validar(nome, descricao, permissoes, Protocolo, TamanhoDoDocumento, catalogo));

    /// <summary>A versão pendente que a proposta sem remoção cria, ou a recusa dela.</summary>
    public static Result<VersaoDoPerfil> CriarVersao(
        Perfil perfil,
        ConteudoDaVersao conteudo,
        ContextoDaVersao contexto,
        AutoriaDaVersao autoria,
        DateTimeOffset instante,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        DesfechoDaVersao desfecho = perfil.Propor(new PropostaDeVersao(conteudo, null, null, autoria, instante), contexto, catalogo);

        return desfecho.Concluida && desfecho.Criada is { } criada
            ? Result<VersaoDoPerfil>.Success(criada)
            : Result<VersaoDoPerfil>.Failure(desfecho.Erro ?? throw new InvalidOperationException("A proposta não criou versão pendente."));
    }

    /// <summary>Perfil comum com a versão 1 pendente, esperando o envio do documento.</summary>
    public static Perfil Pendente(
        string nome, string descricao, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo) =>
        Garantir(Perfil.Criar(Conteudo(nome, descricao, permissoes, catalogo), AutoriaDoAutor, Instante, catalogo));

    /// <summary>Perfil comum com a versão 1 em vigor: o documento foi enviado e verificado.</summary>
    public static Perfil Vigente(
        string nome, string descricao, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo)
    {
        Perfil perfil = Pendente(nome, descricao, permissoes, catalogo);
        EntrarEmVigor(perfil, catalogo);

        return perfil;
    }

    /// <summary>Cria uma versão nova do perfil, com o mesmo nome, e a leva à vigência.</summary>
    public static DesfechoDaVersao NovaVersaoVigente(
        Perfil perfil, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        Garantir(CriarVersao(perfil,
            Conteudo(perfil.Nome, perfil.Descricao, permissoes, catalogo), SemContexto, AutoriaDoAutor, Instante, catalogo));

        return EntrarEmVigor(perfil, catalogo);
    }

    /// <summary>
    /// Leva o perfil à composição pedida pelos caminhos do domínio: retirar permissão é remoção, que vale na hora, e
    /// acrescentar é versão proposta, aprovada. A troca que retira e acrescenta se faz em duas, a remoção primeiro, e
    /// devolve cada troca na ordem em que aconteceu.
    /// </summary>
    public static IReadOnlyList<TrocaDeVigencia> Trocar(
        Perfil perfil, IEnumerable<string> permissoes, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(permissoes);

        string[] desejadas = [.. permissoes];
        string[] retiradas = [.. perfil.ComposicaoEmVigor.Except(desejadas)];
        List<TrocaDeVigencia> trocas = [];
        if (retiradas.Length > 0)
        {
            trocas.Add(perfil.Remover(new PedidoDeRemocao(retiradas, "Permissão concedida por engano.", null, AutoriaDaGestora, Instante)).Troca!);
        }

        if (desejadas.Except(perfil.ComposicaoEmVigor).Any())
        {
            trocas.Add(NovaVersaoVigente(perfil, desejadas, catalogo).Troca!);
        }

        return trocas;
    }

    /// <summary>
    /// Leva a versão pendente do perfil à vigência pelo caminho real: o envio e a verificação do documento, a aprovação
    /// da gestão e, quando a versão exige, a da proteção de dados. Devolve o desfecho da última decisão.
    /// </summary>
    public static DesfechoDaVersao EntrarEmVigor(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = Garantir(perfil.ConfirmarDocumento(versao.Id, "etag-de-teste", Instante));
        perfil.RegistrarDocumentoVerificado(documento.Id, new string('a', 64), Instante);

        DesfechoDaVersao desfecho = Decidir(perfil, versao, EtapaDaVersao.GestaoDeAcessos, AutoriaDaGestora, catalogo);

        return versao.Pendente
            ? Decidir(perfil, versao, EtapaDaVersao.ProtecaoDeDados, AutoriaDaProtecaoDeDados, catalogo)
            : desfecho;
    }

    /// <summary>Aprova a etapa da versão em nome de <paramref name="autoria"/>, sem contexto de concessão.</summary>
    public static DesfechoDaVersao Decidir(
        Perfil perfil, VersaoDoPerfil versao, EtapaDaVersao etapa, AutoriaDaVersao autoria, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(versao);

        return perfil.Decidir(
            new PedidoDeDecisaoDaVersao(versao.Id, etapa, ResultadoDaDecisao.Aprovado, null, autoria, Instante), SemContexto, catalogo);
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
