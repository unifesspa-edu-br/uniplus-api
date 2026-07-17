namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Formato de arquivo aceito para a apresentação de um <see cref="Entities.DocumentoExigido"/>
/// (Story #554, PR-d, issue #893) — congelado na própria exigência, opcional
/// (<see langword="null"/> = sem restrição de formato). O módulo Configuração não mantém
/// catálogo de formatos de arquivo (só classifica o <c>TipoDocumento</c>); por isso a
/// lista é fechada em código, não resolvida por leitura externa.
/// </summary>
public enum FormatoPermitido
{
    Nenhum = 0,
    Pdf = 1,
    Jpeg = 2,
    Png = 3,
}
