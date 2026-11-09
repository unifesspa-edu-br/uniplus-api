namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que uma versão do perfil declara, já validado: nome, descrição, permissões, protocolo do documento instituidor e o
/// tamanho previsto desse documento. Acumula toda violação independente (ADR-0125). A unicidade do nome é de quem
/// grava, com proteção de corrida no índice único.
/// </summary>
public sealed record ConteudoDaVersao
{
    private const int NomeMinLength = 3;
    private const int NomeMaxLength = 120;
    private const int DescricaoMinLength = 3;
    private const int DescricaoMaxLength = 500;
    private const int ProtocoloMinLength = 1;
    private const int ProtocoloMaxLength = 60;

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

    public static Result<ConteudoDaVersao> Validar(
        string? nome,
        string? descricao,
        IEnumerable<string>? permissoes,
        string? protocolo,
        long tamanhoDoDocumento,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        string? nomeNormalizado = Normalizar(nome, NomeMinLength, NomeMaxLength);
        string? descricaoNormalizada = Normalizar(descricao, DescricaoMinLength, DescricaoMaxLength);
        string? protocoloNormalizado = Normalizar(protocolo, ProtocoloMinLength, ProtocoloMaxLength);

        // Códigos repetidos na entrada se consolidam sem recusa.
        string[] codigos = [.. (permissoes ?? []).Select(static c => c?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal)];

        List<FieldError> erros =
        [
            .. ErrosDosTextos(nomeNormalizado, descricaoNormalizada, protocoloNormalizado),
            .. ValidarPermissoes(codigos, catalogo),
            .. DocumentoDaVersao.ValidarTamanho(tamanhoDoDocumento),
        ];

        return erros.Count > 0
            ? Result<ConteudoDaVersao>.ValidationFailure(erros)
            : Result<ConteudoDaVersao>.Success(new ConteudoDaVersao(
                nomeNormalizado!, descricaoNormalizada!, Catalogar(codigos, catalogo), protocoloNormalizado!, tamanhoDoDocumento));
    }

    private static PermissaoCatalogada[] Catalogar(string[] codigos, ICatalogoDePermissoesConsultavel catalogo) =>
        [.. codigos.Select(c => new PermissaoCatalogada(c, catalogo.Sensibilidade(c), catalogo.Existe(c) && catalogo.DeUnidade(c)))];

    private static IEnumerable<FieldError> ErrosDosTextos(string? nome, string? descricao, string? protocolo) =>
        new[]
        {
            nome is null
                ? Erro("nome", PerfilErrorCodes.NomeInvalido, $"O nome do perfil é obrigatório e deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")
                : null,
            descricao is null
                ? Erro("descricao", PerfilErrorCodes.DescricaoInvalida, $"A descrição do perfil é obrigatória e deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres.")
                : null,
            protocolo is null
                ? Erro("protocolo", PerfilErrorCodes.ProtocoloInvalido, $"O protocolo do documento instituidor é obrigatório e deve ter até {ProtocoloMaxLength} caracteres.")
                : null,
        }.OfType<FieldError>();

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

    private static FieldError Erro(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));

    // O caractere nulo não é espaço em branco e cabe no tamanho, mas a coluna textual do banco não o armazena: sem
    // esta recusa o valor só falharia ao gravar, e o payload inválido responderia 500 em vez da validação de domínio.
    private static string? Normalizar(string? texto, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string normalizado = texto.Trim();

        return normalizado.Contains('\0', StringComparison.Ordinal) || normalizado.Length < min || normalizado.Length > max
            ? null
            : normalizado;
    }
}
