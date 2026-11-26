namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A cópia de um modelo de formulário pronta para o processo: o formulário do código informado
/// — título, páginas, itens, grupos repetíveis e termos já conferidos contra o catálogo — e as derivações que o processo
/// ainda não configura, com o modelo de que partiu (ADR-0061).
/// </summary>
public sealed record CopiaDeModeloDeFormulario(
    CodigoDoFormulario Formulario,
    string? Titulo,
    IReadOnlyList<PaginaDoFormulario> Paginas,
    IReadOnlyList<FatoColetado> Itens,
    IReadOnlyList<GrupoColetado> Grupos,
    IReadOnlyList<TermoExigidoFormulario> Termos,
    IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesNovas,
    ModeloDeOrigem Origem)
{
    // Provisório até a retirada da finalidade: o código dos formulários conhecidos dá a finalidade que o planejamento ainda usa.
    internal FinalidadeFormulario Finalidade =>
        EstruturaFormulario.FinalidadeDoCodigo(Formulario.Valor)
        ?? throw new InvalidOperationException("O código da cópia não corresponde a nenhuma finalidade conhecida.");
}
