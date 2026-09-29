namespace Unifesspa.UniPlus.Regras.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Metadado mínimo de um fato do candidato (ADR-0111) de que
/// <see cref="Regras.Services.PredicadoDnfValidador"/> precisa para validar uma
/// condição. As regras sobre fatos dependem só do Kernel (ADR-0135), então este tipo
/// não conhece <c>Unifesspa.UniPlus.Configuracao.Contracts</c>: quem chama o validador,
/// em cada módulo, mapeia para ele o fato que leu do catálogo (<c>FatoCandidatoView</c>).
/// </summary>
/// <remarks>
/// Só representa fatos cujo domínio é genericamente validável por esta
/// Story — <see cref="TipoDominioFato.CategoricoEstatico"/> exige
/// <see cref="ValoresDominio"/> preenchido; um fato categórico de
/// escopo-processo (domínio dinâmico, <c>ValoresDominio</c> nulo no catálogo
/// de origem) não é representável aqui e fica fora do vocabulário fechado
/// desta Story.
/// </remarks>
public sealed record DescritorFatoCandidato
{
    private DescritorFatoCandidato(string codigo, TipoDominioFato tipoDominio, IReadOnlyList<string>? valoresDominio)
    {
        Codigo = codigo;
        TipoDominio = tipoDominio;
        ValoresDominio = valoresDominio;
    }

    public string Codigo { get; }

    public TipoDominioFato TipoDominio { get; }

    public IReadOnlyList<string>? ValoresDominio { get; }

    /// <summary>
    /// Cria o descritor validando a coerência tudo-nulo/domínio-declarado:
    /// <see cref="TipoDominioFato.CategoricoEstatico"/> exige
    /// <see cref="ValoresDominio"/> preenchido; qualquer outro domínio exige
    /// que ele seja nulo/vazio.
    /// </summary>
    public static Result<DescritorFatoCandidato> Criar(
        string codigo, TipoDominioFato tipoDominio, IReadOnlyList<string>? valoresDominio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        bool exigeDominio = tipoDominio == TipoDominioFato.CategoricoEstatico;
        bool dominioPreenchido = valoresDominio is { Count: > 0 };

        if (exigeDominio != dominioPreenchido)
        {
            return Result<DescritorFatoCandidato>.Failure(new DomainError(
                "DescritorFatoCandidato.DominioIncoerente",
                exigeDominio
                    ? $"O fato '{codigo}' é categórico estático e exige ValoresDominio preenchido."
                    : $"O fato '{codigo}' não é categórico estático — ValoresDominio deve ser nulo."));
        }

        return Result<DescritorFatoCandidato>.Success(new DescritorFatoCandidato(codigo.Trim(), tipoDominio, valoresDominio));
    }
}
