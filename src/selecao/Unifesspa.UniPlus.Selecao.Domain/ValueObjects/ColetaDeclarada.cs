namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>O que o processo coleta do candidato: os campos, os grupos repetíveis, os formulários, os termos, as opções, as regras de derivação e a âncora de FAIXA_ETARIA.</summary>
public sealed class ColetaDeclarada
{
    public ColetaDeclarada(
        IReadOnlyList<FatoColetado> campos,
        IReadOnlyList<GrupoColetado> gruposColetados,
        IReadOnlyList<FormularioProcesso> formularios,
        IReadOnlyList<TermoExigidoFormulario> termosExigidos,
        IReadOnlyList<OpcaoDeclaradaFato> opcoesDeclaradas,
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        ReferenciaTemporalFatos? referenciaTemporalFatos)
    {
        ArgumentNullException.ThrowIfNull(campos);
        ArgumentNullException.ThrowIfNull(gruposColetados);
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(termosExigidos);
        ArgumentNullException.ThrowIfNull(opcoesDeclaradas);
        ArgumentNullException.ThrowIfNull(regrasDerivacao);
        Campos = [.. campos];
        GruposColetados = [.. gruposColetados];
        Formularios = [.. formularios];
        TermosExigidos = [.. termosExigidos];
        OpcoesDeclaradas = [.. opcoesDeclaradas];
        RegrasDerivacao = [.. regrasDerivacao];
        ReferenciaTemporalFatos = referenciaTemporalFatos;
    }

    public IReadOnlyList<FatoColetado> Campos { get; }

    public IReadOnlyList<GrupoColetado> GruposColetados { get; }

    public IReadOnlyList<FormularioProcesso> Formularios { get; }

    public IReadOnlyList<TermoExigidoFormulario> TermosExigidos { get; }

    public IReadOnlyList<OpcaoDeclaradaFato> OpcoesDeclaradas { get; }

    public IReadOnlyList<ConfiguracaoDerivacaoFato> RegrasDerivacao { get; }

    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; }

    /// <summary>Os itens dos formulários: os campos fora de grupo repetível.</summary>
    public IEnumerable<FatoColetado> Itens => Campos.Where(static f => f.GrupoColetadoId is null);

    /// <summary>O formulário da finalidade, quando o processo o tem.</summary>
    public FormularioProcesso? FormularioDe(FinalidadeFormulario finalidade) =>
        Formularios.FirstOrDefault(f => f.Finalidade == finalidade);
}
