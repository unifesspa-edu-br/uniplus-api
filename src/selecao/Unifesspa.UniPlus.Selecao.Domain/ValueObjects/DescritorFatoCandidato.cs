namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Contrato de leitura, próprio do Domain de Selecao, do vocabulário fechado
/// de fatos do candidato (ADR-0111): o metadado mínimo que
/// <see cref="Services.PredicadoDnfValidador"/> precisa para validar uma
/// condição — sem depender de <c>Unifesspa.UniPlus.Configuracao.Contracts</c>
/// (Domain só depende de SharedKernel). Quem chama o validador (Application)
/// mapeia <c>FatoCandidatoView</c> (o DTO cross-módulo real, entregue pelo
/// leitor do #846) para este tipo.
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
