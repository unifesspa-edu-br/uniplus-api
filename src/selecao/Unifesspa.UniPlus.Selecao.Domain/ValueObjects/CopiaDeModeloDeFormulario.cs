namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A cópia de um modelo de formulário pronta para o processo: o formulário da finalidade do modelo
/// — título, etapas, itens e termos já conferidos contra o catálogo — e as derivações que o processo
/// ainda não configura, com o modelo de que partiu (ADR-0061).
/// </summary>
public sealed record CopiaDeModeloDeFormulario(
    FinalidadeFormulario Finalidade,
    string? Titulo,
    IReadOnlyList<EtapaFormulario> Etapas,
    IReadOnlyList<FatoColetado> Itens,
    IReadOnlyList<TermoExigidoFormulario> Termos,
    IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesNovas,
    Guid ModeloId,
    string ModeloCodigo);
