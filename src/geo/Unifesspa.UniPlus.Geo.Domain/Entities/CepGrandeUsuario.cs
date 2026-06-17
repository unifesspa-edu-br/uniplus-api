namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// CEP exclusivo de grande usuário (origem <c>log_grande_usuario</c> da DNE) —
/// órgão/empresa com CEP próprio (ex.: UNESP). Reference data sem soft-delete
/// (ADR-0092). Carrega só <see cref="Cep"/> + <see cref="Nome"/> (+ proveniência):
/// <strong>não tem cidade/UF</strong> — a localização é resolvida por faixa de CEP
/// no lookup (F4), não por FK aqui.
/// </summary>
/// <remarks>O <see cref="Cep"/> é a chave natural UNIQUE (um grande usuário por CEP).</remarks>
public sealed class CepGrandeUsuario : EntityBase
{
    /// <summary>CEP exclusivo (chave natural UNIQUE).</summary>
    public string Cep { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Nome sem acentos (origem <c>*_sem_acento</c>), quando informado.</summary>
    public string? NomeNormalizado { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando o CEP some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private CepGrandeUsuario()
    {
    }

    /// <summary>
    /// Importa um CEP de grande usuário. Valores já tipados (parse no ETL). Valida o
    /// mínimo: chave natural (<paramref name="cep"/>), <paramref name="nome"/> e
    /// proveniência.
    /// </summary>
    public static Result<CepGrandeUsuario> Importar(
        string cep,
        string nome,
        string? nomeNormalizado,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cep);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (string.IsNullOrWhiteSpace(cep))
        {
            return Result<CepGrandeUsuario>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CepGrandeUsuarioCepObrigatorio,
                "CEP do grande usuário é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<CepGrandeUsuario>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CepGrandeUsuarioNomeObrigatorio,
                "Nome do grande usuário é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<CepGrandeUsuario>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CepGrandeUsuarioVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do grande usuário é obrigatória."));
        }

        var entidade = new CepGrandeUsuario
        {
            Cep = cep.Trim(),
            Nome = nome.Trim(),
            NomeNormalizado = GeoTexto.NormalizarBuscaOpcional(nomeNormalizado),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<CepGrandeUsuario>.Success(entidade);
    }
}
