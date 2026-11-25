namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um termo de consentimento ou declaração que o formulário exige (UNI-REQ-0086): escolhido no
/// catálogo por termo e versão, nunca digitado no processo, com exibição e obrigatoriedade que
/// podem depender das respostas do candidato (UNI-REQ-0145).
/// </summary>
/// <remarks>
/// O conteúdo da versão — código do termo, número da versão, nome, texto, base legal, forma de aceite e hash — é copiado quando o
/// termo é escolhido. A versão promovida é imutável no catálogo, então a cópia é o que o edital
/// congela, e mudar o catálogo depois não altera o processo (ADR-0061).
/// </remarks>
public sealed class TermoExigidoFormulario : EntityBase, IParteDoProcesso
{
    /// <summary>Tamanho máximo do código do termo no catálogo.</summary>
    public const int TermoCodigoMaxLength = 80;

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>A finalidade do formulário que exige o termo; atribuída pelo processo.</summary>
    public FinalidadeFormulario? Finalidade { get; private set; }

    /// <summary>Identificador próprio da exigência no formulário, estável mesmo entre termos de nome igual.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Posição do termo no formulário, única no formulário.</summary>
    public int Ordem { get; private set; }

    public Guid TermoId { get; private set; }

    public Guid VersaoId { get; private set; }

    /// <summary>Código estável do termo no catálogo, congelado com a versão: é o que o documento de configuração cita.</summary>
    public string TermoCodigo { get; private set; } = string.Empty;

    /// <summary>Número da versão dentro do termo, congelado com ela, a partir de 1.</summary>
    public int VersaoNumero { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Texto { get; private set; } = string.Empty;

    public string BaseLegal { get; private set; } = string.Empty;

    /// <summary>Forma de aceite da versão do catálogo.</summary>
    public FormaAceite FormaAceite { get; private set; }

    public string HashVersao { get; private set; } = string.Empty;

    /// <summary>Quando o termo aparece; <see langword="null"/> é sempre.</summary>
    public PredicadoDnf? Exibicao { get; private set; }

    public Obrigatoriedade Obrigatoriedade { get; private set; } = Obrigatoriedade.Sempre;

    private TermoExigidoFormulario() { }

    private TermoExigidoFormulario(
        string codigo,
        int ordem,
        VersaoTermoEscolhida versao,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        FinalidadeFormulario? finalidade)
    {
        Codigo = codigo ?? string.Empty;
        Ordem = ordem;
        CongelarVersao(versao);
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        // O processo atribui a finalidade ao definir os termos; quem reconstrói o registro publicado a informa.
        Finalidade = finalidade;
    }

    private void CongelarVersao(VersaoTermoEscolhida versao)
    {
        TermoId = versao.TermoId;
        VersaoId = versao.VersaoId;
        TermoCodigo = versao.TermoCodigo;
        VersaoNumero = versao.VersaoNumero;
        Nome = versao.Nome;
        Texto = versao.Texto;
        BaseLegal = versao.BaseLegal;
        FormaAceite = versao.FormaAceite;
        HashVersao = versao.Hash;
    }

    /// <summary>Os fatos que as condições do termo citam, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];

    /// <summary>O termo utilizado na conferência das dependências do formulário.</summary>
    public TermoDoGrafo ParaGrafo() => new(Codigo, FatosCitados);

    /// <summary>A versão e o conteúdo fixados para conferir o aceite.</summary>
    public TermoAdmitido ParaAceite() => new(VersaoId, HashVersao);

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
        Obrigatoriedade obrigatoriedade,
        FinalidadeFormulario? finalidade = null)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        List<FieldError> erros = FormaDoTermo.ValidarFormaBasica(codigo, ordem);
        if (erros.Count > 0)
        {
            return Result<TermoExigidoFormulario>.ValidationFailure(erros);
        }

        return Result<TermoExigidoFormulario>.Success(new TermoExigidoFormulario(codigo, ordem, versao, exibicao, obrigatoriedade, finalidade));
    }

    /// <summary>A forma de aceite ainda não foi definida no catálogo: o termo não pode ser publicado.</summary>
    public bool SemFormaDeAceite => FormaAceite == FormaAceite.ADefinir;

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) => ProcessoSeletivoId = processoSeletivoId;

    internal void VincularFinalidade(FinalidadeFormulario finalidade) => Finalidade = finalidade;
}

/// <summary>A versão do catálogo escolhida para o termo, com o conteúdo que o processo congela.</summary>
public sealed record VersaoTermoEscolhida(
    Guid TermoId, Guid VersaoId, string TermoCodigo, int VersaoNumero, string Nome, string Texto, string BaseLegal, FormaAceite FormaAceite, string Hash);

public static class TermoExigidoFormularioErrorCodes
{
    public const string ObrigatoriedadeInvalida = "TermoExigidoFormulario.ObrigatoriedadeInvalida";
    public const string VersaoNaoEncontrada = "TermoExigidoFormulario.VersaoNaoEncontrada";
    public const string EntradaMalformada = "TermoExigidoFormulario.EntradaMalformada";
    public const string SemFormaDeAceite = "ProcessoSeletivo.TermoExigidoSemFormaDeAceite";
}
