namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um termo de consentimento ou declaração que o formulário exige (UNI-REQ-0086): escolhido no
/// catálogo por termo e versão, nunca digitado no processo, com exibição e obrigatoriedade que
/// podem depender das respostas do candidato (UNI-REQ-0145).
/// </summary>
/// <remarks>
/// O conteúdo da versão — nome, texto, base legal, forma de aceite e hash — é copiado quando o
/// termo é escolhido. A versão promovida é imutável no catálogo, então a cópia é o que o edital
/// congela, e mudar o catálogo depois não altera o processo (ADR-0061).
/// </remarks>
public sealed class TermoExigidoFormulario : EntityBase
{
    public const int CodigoMaxLength = 60;

    /// <summary>A forma de aceite que o catálogo ainda não definiu: a publicação a recusa.</summary>
    public const string FormaAceiteADefinir = "A_DEFINIR";

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Identificador próprio da exigência no formulário, estável mesmo entre termos de nome igual.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Posição do termo no formulário, única no processo.</summary>
    public int Ordem { get; private set; }

    public Guid TermoId { get; private set; }

    public Guid VersaoId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Texto { get; private set; } = string.Empty;

    public string BaseLegal { get; private set; } = string.Empty;

    /// <summary>Forma de aceite da versão, em token canônico do catálogo.</summary>
    public string FormaAceite { get; private set; } = string.Empty;

    public string HashVersao { get; private set; } = string.Empty;

    /// <summary>Quando o termo aparece; <see langword="null"/> é sempre.</summary>
    public PredicadoDnf? Exibicao { get; private set; }

    public Obrigatoriedade Obrigatoriedade { get; private set; } = Obrigatoriedade.Sempre;

    private TermoExigidoFormulario() { }

    /// <summary>Os fatos que as condições do termo citam, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>As condições do termo, para os vínculos e as referências a valor do processo.</summary>
    public IEnumerable<CondicaoDnf> Condicoes =>
        (Exibicao?.Clausulas ?? []).Concat(Obrigatoriedade.Predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes);

    /// <summary>
    /// Acumula as violações de forma (ADR-0125). A versão chega já resolvida no catálogo; a
    /// unicidade de código e de ordem é do formulário.
    /// </summary>
    public static Result<TermoExigidoFormulario> Criar(
        string codigo,
        int ordem,
        VersaoTermoEscolhida versao,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        List<FieldError> erros = ValidarFormaBasica(codigo, ordem);
        if (erros.Count > 0)
        {
            return Result<TermoExigidoFormulario>.ValidationFailure(erros);
        }

        return Result<TermoExigidoFormulario>.Success(new TermoExigidoFormulario
        {
            // O envelope congela o código em NFC; normalizar aqui faz a unicidade do formulário e a
            // do envelope compararem a mesma forma.
            Codigo = codigo.Trim().Normalize(System.Text.NormalizationForm.FormC),
            Ordem = ordem,
            TermoId = versao.TermoId,
            VersaoId = versao.VersaoId,
            Nome = versao.Nome,
            Texto = versao.Texto,
            BaseLegal = versao.BaseLegal,
            FormaAceite = versao.FormaAceite,
            HashVersao = versao.Hash,
            Exibicao = exibicao,
            Obrigatoriedade = obrigatoriedade,
        });
    }

    /// <summary>A forma do termo que não depende do catálogo: confere todo o payload antes de ler as versões.</summary>
    public static List<FieldError> ValidarFormaBasica(string? codigo, int ordem)
    {
        List<FieldError> erros = [];
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(TermoExigidoFormularioErrorCodes.CodigoObrigatorio, "O código do termo exigido é obrigatório.")));
        }
        else if (codigo.Trim().Length > CodigoMaxLength)
        {
            erros.Add(new("codigo", new DomainError(
                TermoExigidoFormularioErrorCodes.CodigoTamanho, $"O código do termo exigido deve ter no máximo {CodigoMaxLength} caracteres.")));
        }

        if (ordem < 0)
        {
            erros.Add(new("ordem", new DomainError(TermoExigidoFormularioErrorCodes.OrdemInvalida, "A ordem do termo não pode ser negativa.")));
        }

        return erros;
    }

    /// <summary>
    /// Código e ordem únicos entre os termos do formulário, com o código na forma que o termo guarda
    /// (aparado, em NFC). Sem efeito colateral: o handler a confere junto das outras recusas, e o
    /// processo, de novo, antes de trocar os termos. Termo ausente ou sem código não entra na
    /// conferência do código — a recusa dele é a de forma.
    /// </summary>
    public static List<FieldError> ConferirUnicidade(IReadOnlyList<(string? Codigo, int Ordem)?> termos)
    {
        ArgumentNullException.ThrowIfNull(termos);

        List<FieldError> erros = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        for (int i = 0; i < termos.Count; i++)
        {
            if (termos[i] is not { } termo)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(termo.Codigo)
                && !codigos.Add(termo.Codigo.Trim().Normalize(System.Text.NormalizationForm.FormC)))
            {
                erros.Add(new($"termos[{i}].codigo", new DomainError(
                    TermoExigidoFormularioErrorCodes.CodigoDuplicado, $"O código '{termo.Codigo.Trim()}' aparece em mais de um termo do formulário.")));
            }

            if (!ordens.Add(termo.Ordem))
            {
                erros.Add(new($"termos[{i}].ordem", new DomainError(
                    TermoExigidoFormularioErrorCodes.OrdemDuplicada, "Dois termos do formulário têm a mesma ordem.")));
            }
        }

        return erros;
    }

    /// <summary>A forma de aceite ainda não foi definida no catálogo: o termo não pode ser publicado.</summary>
    public bool SemFormaDeAceite => string.Equals(FormaAceite, FormaAceiteADefinir, StringComparison.Ordinal);

    internal void VincularProcessoSeletivo(Guid processoSeletivoId) => ProcessoSeletivoId = processoSeletivoId;
}

/// <summary>A versão do catálogo escolhida para o termo, com o conteúdo que o processo congela.</summary>
public sealed record VersaoTermoEscolhida(
    Guid TermoId, Guid VersaoId, string Nome, string Texto, string BaseLegal, string FormaAceite, string Hash);

public static class TermoExigidoFormularioErrorCodes
{
    public const string CodigoObrigatorio = "TermoExigidoFormulario.CodigoObrigatorio";
    public const string CodigoTamanho = "TermoExigidoFormulario.CodigoTamanho";
    public const string OrdemInvalida = "TermoExigidoFormulario.OrdemInvalida";
    public const string CodigoDuplicado = "TermoExigidoFormulario.CodigoDuplicado";
    public const string OrdemDuplicada = "TermoExigidoFormulario.OrdemDuplicada";
    public const string ObrigatoriedadeInvalida = "TermoExigidoFormulario.ObrigatoriedadeInvalida";
    public const string VersaoNaoEncontrada = "TermoExigidoFormulario.VersaoNaoEncontrada";
    public const string EntradaMalformada = "TermoExigidoFormulario.EntradaMalformada";
    public const string SemFormaDeAceite = "ProcessoSeletivo.TermoExigidoSemFormaDeAceite";
}
