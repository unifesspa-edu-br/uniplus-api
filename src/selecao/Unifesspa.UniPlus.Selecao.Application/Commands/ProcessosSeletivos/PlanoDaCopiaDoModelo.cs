namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Tudo o que a aplicação do modelo calculou antes de mudar o processo: a cópia que o agregado confere, a
/// posição de cada parte no modelo, o relatório e o que o conjunto de documentos ainda precisa.
/// </summary>
internal sealed record PlanoDaCopiaDoModelo(
    CopiaDeModeloDeFormulario Copia,
    IndicesNoModelo Indices,
    RelatorioDaAplicacaoDoModelo Relatorio,
    FinalidadeFormulario Finalidade,
    IReadOnlyList<FatoCandidatoView> Catalogo,
    IReadOnlyList<DocumentoExigidoDoModeloInput> Documentos);
