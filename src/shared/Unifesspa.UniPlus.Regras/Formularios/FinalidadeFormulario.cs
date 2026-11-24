namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Para que o formulário coleta respostas: cada processo tem no máximo um por finalidade (UNI-REQ-0144). O formulário
/// sem finalidade não existe: onde a finalidade pode faltar, o valor é anulável.
/// </summary>
[DescricaoDoContrato("Para que o formulário coleta respostas: cada processo tem no máximo um formulário por finalidade.")]
public enum FinalidadeFormulario
{
    /// <summary>Formulário em que o candidato se inscreve no processo.</summary>
    [CodigoDeContrato("INSCRICAO", "Inscrição", Descricao = "Formulário em que o candidato se inscreve no processo seletivo.")]
    Inscricao = 1,

    /// <summary>Formulário em que o candidato pede a isenção da taxa de inscrição.</summary>
    [CodigoDeContrato("ISENCAO_TAXA", "Isenção da taxa", Descricao = "Formulário em que o candidato pede a isenção da taxa de inscrição.")]
    IsencaoTaxa = 2,

    /// <summary>Formulário em que o candidato comprova os requisitos para ser habilitado.</summary>
    [CodigoDeContrato("HABILITACAO", "Habilitação", Descricao = "Formulário em que o candidato comprova os requisitos para ser habilitado.")]
    Habilitacao = 3,
}
