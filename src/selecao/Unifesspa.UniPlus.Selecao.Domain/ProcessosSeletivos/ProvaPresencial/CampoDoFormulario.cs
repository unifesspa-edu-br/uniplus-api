namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Um campo coletado por um formulário do processo, no que decide se ele é pedido a todo candidato: o
/// formulário que o traz, e se é obrigatório sempre, sem pré-condição, fora de grupo e em seção que não se
/// esconde. Os quatro juntos são a pergunta a todo candidato (ADR-0171).
/// </summary>
public sealed record CampoDoFormulario(
    CodigoDoFormulario Formulario, bool ObrigatorioSempre, bool SemPrecondicao, bool ForaDeGrupo, bool EmSecaoSempreVisivel)
{
    /// <summary>Se todo candidato que responde o formulário responde o campo.</summary>
    public bool PedidoATodoCandidato => ObrigatorioSempre && SemPrecondicao && ForaDeGrupo && EmSecaoSempreVisivel;
}
