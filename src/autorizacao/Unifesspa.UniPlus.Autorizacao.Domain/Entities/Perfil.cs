namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Perfil de acesso: um nome e um conjunto de permissões do catálogo que a concessão
/// atribui de uma vez a quem exerce uma função.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sensibilidade do perfil não é gravada.</b> Ela é a mais alta entre as permissões que
/// o perfil reúne e se deriva do catálogo a cada leitura: gravá-la copiaria um dado de que o
/// catálogo é dono e o deixaria envelhecer quando a sensibilidade de uma permissão mudar.
/// </para>
/// <para>
/// <b>Perfil não se exclui, se desativa.</b> Desativar não revoga concessão alguma; só impede
/// pedidos novos, regra conferida por quem recebe o pedido.
/// </para>
/// <para>
/// <b>Perfis de governança</b> (<see cref="Governanca"/> preenchida) são de sistema: nascem da
/// carga inicial, são os únicos que podem reunir permissões reservadas à governança e não se
/// editam, ativam nem desativam por esta API.
/// </para>
/// </remarks>
public sealed class Perfil : EntityBase
{
    private const int NomeMinLength = 3;
    private const int NomeMaxLength = 120;
    private const int DescricaoMinLength = 3;
    private const int DescricaoMaxLength = 500;

    private readonly List<PermissaoDoPerfil> _permissoes = [];

    public string Nome { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    public bool Ativo { get; private set; } = true;

    /// <summary>Papel de governança do perfil de sistema; nulo nos perfis comuns.</summary>
    public TipoDeGovernanca? Governanca { get; private set; }

    public IReadOnlyCollection<PermissaoDoPerfil> Permissoes => _permissoes;

    // Construtor de materialização do EF Core.
    private Perfil()
    {
    }

    /// <summary>
    /// Cria um perfil comum, já ativo. Acumula toda violação independente (ADR-0125): nome,
    /// descrição e permissões inválidos coexistem na mesma resposta. A unicidade do nome é do
    /// handler, com proteção de corrida no índice único.
    /// </summary>
    public static Result<Perfil> Criar(
        string? nome,
        string? descricao,
        IEnumerable<string>? permissoes,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        Result<CamposValidados> campos = ValidarCampos(nome, descricao, permissoes, catalogo);
        if (campos.IsFailure)
        {
            return Result<Perfil>.ValidationFailure(campos.Errors);
        }

        Perfil perfil = new();
        perfil.AplicarCampos(campos.Value!);

        return Result<Perfil>.Success(perfil);
    }

    /// <summary>Substitui nome, descrição e permissões do perfil comum.</summary>
    public Result Atualizar(
        string? nome,
        string? descricao,
        IEnumerable<string>? permissoes,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        if (Governanca is not null)
        {
            return Result.Failure(GovernancaImutavel());
        }

        Result<CamposValidados> campos = ValidarCampos(nome, descricao, permissoes, catalogo);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(campos.Value!);

        return Result.Success();
    }

    /// <summary>Reativa o perfil. Reativar o que já está ativo não muda nada.</summary>
    public Result Ativar()
    {
        if (Governanca is not null)
        {
            return Result.Failure(GovernancaImutavel());
        }

        Ativo = true;

        return Result.Success();
    }

    /// <summary>Desativa o perfil. Desativar o que já está inativo não muda nada.</summary>
    public Result Desativar()
    {
        if (Governanca is not null)
        {
            return Result.Failure(GovernancaImutavel());
        }

        Ativo = false;

        return Result.Success();
    }

    private static Result<CamposValidados> ValidarCampos(
        string? nome,
        string? descricao,
        IEnumerable<string>? permissoes,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        List<FieldError> erros = [];

        string? nomeNormalizado = Normalizar(nome, NomeMinLength, NomeMaxLength);
        if (nomeNormalizado is null)
        {
            erros.Add(new("nome", new DomainError(
                PerfilErrorCodes.NomeInvalido,
                $"O nome do perfil é obrigatório e deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
        }

        string? descricaoNormalizada = Normalizar(descricao, DescricaoMinLength, DescricaoMaxLength);
        if (descricaoNormalizada is null)
        {
            erros.Add(new("descricao", new DomainError(
                PerfilErrorCodes.DescricaoInvalida,
                $"A descrição do perfil é obrigatória e deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres.")));
        }

        // Códigos repetidos na entrada se consolidam sem recusa.
        string[] codigos = [.. (permissoes ?? []).Select(static c => c?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal)];
        erros.AddRange(ValidarPermissoes(codigos, catalogo));

        return erros.Count > 0
            ? Result<CamposValidados>.ValidationFailure(erros)
            : Result<CamposValidados>.Success(new CamposValidados(nomeNormalizado!, descricaoNormalizada!, codigos));
    }

    private static IEnumerable<FieldError> ValidarPermissoes(string[] codigos, ICatalogoDePermissoesConsultavel catalogo)
    {
        if (codigos.Length == 0)
        {
            yield return new("permissoes", new DomainError(
                PerfilErrorCodes.SemPermissao,
                "O perfil deve reunir ao menos uma permissão."));
            yield break;
        }

        string[] inexistentes = [.. codigos.Where(c => !catalogo.Existe(c))];
        if (inexistentes.Length > 0)
        {
            yield return new("permissoes", new DomainError(
                PerfilErrorCodes.PermissaoInexistente,
                $"Permissões fora do catálogo: {string.Join(", ", inexistentes)}."));
        }

        // O perfil comum nunca reúne permissão reservada à governança. Os perfis de
        // governança nascem da carga inicial, não desta operação.
        string[] reservadas = [.. codigos.Where(c => catalogo.Existe(c) && catalogo.ReservadaAGovernanca(c))];
        if (reservadas.Length > 0)
        {
            yield return new("permissoes", new DomainError(
                PerfilErrorCodes.PermissaoReservada,
                $"Permissões reservadas aos perfis de governança: {string.Join(", ", reservadas)}."));
        }
    }

    // O caractere nulo não é espaço em branco e cabe no tamanho, mas a coluna textual do
    // banco não o armazena: sem esta recusa o valor só falharia ao gravar, e o payload
    // inválido responderia 500 em vez da validação de domínio.
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

    private static DomainError GovernancaImutavel() => new(
        PerfilErrorCodes.GovernancaImutavel,
        "Perfil de governança é de sistema e não pode ser editado, ativado nem desativado.");

    private void AplicarCampos(CamposValidados campos)
    {
        Nome = campos.Nome;
        Descricao = campos.Descricao;

        // Diferença em vez de limpar e reinserir: a chave da permissão é o próprio código,
        // e remover e inserir a mesma chave numa só gravação colide no rastreamento do EF.
        _permissoes.RemoveAll(p => !campos.Permissoes.Contains(p.Codigo, StringComparer.Ordinal));
        foreach (string codigo in campos.Permissoes.Where(c => !_permissoes.Exists(p => p.Codigo == c)))
        {
            _permissoes.Add(new PermissaoDoPerfil(codigo));
        }
    }

    private sealed record CamposValidados(string Nome, string Descricao, string[] Permissoes);
}
