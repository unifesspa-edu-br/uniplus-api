namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que uma versão do perfil declara, já validado: nome, descrição, permissões, protocolo do documento instituidor e o
/// tamanho previsto desse documento. Acumula toda violação independente (ADR-0125). A unicidade do nome é de quem
/// grava, com proteção de corrida no índice único.
/// </summary>
public sealed record ConteudoDaVersao
{
    public string Nome { get; }

    public string Descricao { get; }

    public IReadOnlyList<PermissaoCatalogada> Permissoes { get; }

    public string Protocolo { get; }

    public long TamanhoDoDocumento { get; }

    private ConteudoDaVersao(
        string nome, string descricao, IReadOnlyList<PermissaoCatalogada> permissoes, string protocolo, long tamanhoDoDocumento)
    {
        Nome = nome;
        Descricao = descricao;
        Permissoes = permissoes;
        Protocolo = protocolo;
        TamanhoDoDocumento = tamanhoDoDocumento;
    }

    /// <summary>Códigos das permissões da versão, ordenados.</summary>
    public IReadOnlyList<string> Codigos() => [.. Permissoes.Select(static p => p.Codigo.Valor).Order(StringComparer.Ordinal)];

    public static Result<ConteudoDaVersao> Validar(
        string? nome,
        string? descricao,
        IEnumerable<string>? permissoes,
        string? protocolo,
        long tamanhoDoDocumento,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        TextosDaVersao textos = TextosDaVersao.Normalizar(nome, descricao, protocolo);
        string[] codigos = CodigosConsolidados(permissoes);
        List<FieldError> erros = ErrosDoConteudo(textos, codigos, tamanhoDoDocumento, catalogo);

        return erros.Count > 0
            ? Result<ConteudoDaVersao>.ValidationFailure(erros)
            : Result<ConteudoDaVersao>.Success(new ConteudoDaVersao(
                textos.Nome!.Valor, textos.Descricao!, Catalogar(codigos, catalogo), textos.Protocolo!, tamanhoDoDocumento));
    }

    internal static FieldError Erro(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));

    /// <summary>A ordem dos erros do lote: textos, permissões, tamanho do documento (ADR-0125).</summary>
    private static List<FieldError> ErrosDoConteudo(
        TextosDaVersao textos, string[] codigos, long tamanhoDoDocumento, ICatalogoDePermissoesConsultavel catalogo) =>
        [
            .. textos.Erros(),
            .. ValidarPermissoes(codigos, catalogo),
            .. DocumentoDaVersao.ValidarTamanho(tamanhoDoDocumento),
        ];

    /// <summary>Os códigos da entrada sem espaços nas pontas; os repetidos se consolidam sem recusa.</summary>
    private static string[] CodigosConsolidados(IEnumerable<string>? permissoes) =>
        [.. (permissoes ?? []).Select(static c => c?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal)];

    private static PermissaoCatalogada[] Catalogar(string[] codigos, ICatalogoDePermissoesConsultavel catalogo) =>
        [.. codigos.Select(c => new PermissaoCatalogada(CodigoDePermissao.De(c), catalogo.Sensibilidade(c), catalogo.Existe(c) && catalogo.DeUnidade(c)))];

    private static IEnumerable<FieldError> ValidarPermissoes(string[] codigos, ICatalogoDePermissoesConsultavel catalogo) =>
        codigos.Length == 0
            ? [Erro("permissoes", PerfilErrorCodes.SemPermissao, "O perfil deve reunir ao menos uma permissão.")]
            : new[] { Inexistentes(codigos, catalogo), AlcanceMisto(codigos, catalogo), Reservadas(codigos, catalogo) }.OfType<FieldError>();

    private static FieldError? Inexistentes(string[] codigos, ICatalogoDePermissoesConsultavel catalogo)
    {
        string[] inexistentes = [.. codigos.Where(c => !catalogo.Existe(c))];

        return inexistentes.Length > 0
            ? Erro("permissoes", PerfilErrorCodes.PermissaoInexistente, $"Permissões fora do catálogo: {string.Join(", ", inexistentes)}.")
            : null;
    }

    // Permissão de unidade e permissão institucional não se concedem no mesmo escopo: o perfil misto não teria escopo
    // que o conceda por inteiro.
    private static FieldError? AlcanceMisto(string[] codigos, ICatalogoDePermissoesConsultavel catalogo) =>
        AlcanceDasPermissoes.De(codigos, catalogo) is null
            ? Erro("permissoes", PerfilErrorCodes.AlcanceMisto, "O perfil não pode misturar permissões de unidade e permissões institucionais.")
            : null;

    // O perfil comum nunca reúne permissão reservada à governança. Os perfis de governança nascem da carga inicial, não
    // desta operação.
    private static FieldError? Reservadas(string[] codigos, ICatalogoDePermissoesConsultavel catalogo)
    {
        string[] reservadas = [.. codigos.Where(c => catalogo.Existe(c) && catalogo.ReservadaAGovernanca(c))];

        return reservadas.Length > 0
            ? Erro("permissoes", PerfilErrorCodes.PermissaoReservada, $"Permissões reservadas aos perfis de governança: {string.Join(", ", reservadas)}.")
            : null;
    }
}
