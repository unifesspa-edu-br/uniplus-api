namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Tudo o que a aplicação do modelo calculou antes de mudar o processo: a cópia que o agregado confere, a
/// posição de cada parte no modelo, o relatório e o catálogo de fatos que o conjunto de documentos ainda consulta.
/// </summary>
internal sealed record PlanoDaCopiaDoModelo(
    CopiaDeModeloDeFormulario Copia,
    IndicesNoModelo Indices,
    RelatorioDaAplicacaoDoModelo Relatorio,
    IReadOnlyList<FatoCandidatoView> Catalogo);
