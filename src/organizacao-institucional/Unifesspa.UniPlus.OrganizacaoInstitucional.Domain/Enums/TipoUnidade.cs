namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Classificação organizacional de uma Unidade institucional da Unifesspa (ADR-0055).
/// Roster fechado de 11 valores — novos tipos exigem deliberação do Tech Lead.
/// O código de cada membro é o valor gravado na coluna <c>tipo</c>; o valor numérico continua no contrato do
/// filtro da listagem: nunca renumerar existentes.
/// </summary>
[DescricaoDoContrato("O tipo de uma unidade da universidade, que a classifica na estrutura organizacional: da reitoria aos núcleos.")]
public enum TipoUnidade
{
    [CodigoDeContrato("REITORIA", "Reitoria", Descricao = "A unidade máxima de administração da universidade.")]
    Reitoria = 1,
    [CodigoDeContrato("PRO_REITORIA", "Pró-reitoria", Descricao = "Unidade de administração superior ligada à reitoria e responsável por uma área, como ensino ou pesquisa.")]
    ProReitoria = 2,
    [CodigoDeContrato("CENTRO", "Centro", Descricao = "Unidade que reúne cursos e áreas de conhecimento afins.")]
    Centro = 3,
    [CodigoDeContrato("INSTITUTO", "Instituto", Descricao = "Unidade acadêmica que reúne cursos e pesquisa de uma área do conhecimento.")]
    Instituto = 4,
    [CodigoDeContrato("FACULDADE", "Faculdade", Descricao = "Unidade acadêmica responsável por cursos de uma área específica.")]
    Faculdade = 5,
    [CodigoDeContrato("DEPARTAMENTO", "Departamento", Descricao = "Subdivisão de uma unidade acadêmica que reúne disciplinas afins.")]
    Departamento = 6,
    [CodigoDeContrato("COORDENACAO", "Coordenação", Descricao = "Unidade que coordena um curso, um programa ou uma atividade.")]
    Coordenacao = 7,
    [CodigoDeContrato("DIRETORIA", "Diretoria", Descricao = "Unidade de direção de um conjunto de atividades administrativas ou acadêmicas.")]
    Diretoria = 8,
    [CodigoDeContrato("DIVISAO", "Divisão", Descricao = "Subdivisão de uma diretoria ou de outra unidade administrativa.")]
    Divisao = 9,
    [CodigoDeContrato("NUCLEO", "Núcleo", Descricao = "Unidade que reúne pessoas em torno de um tema ou de uma atividade específica.")]
    Nucleo = 10,
    [CodigoDeContrato("OUTRO", "Outro", Descricao = "Unidade que não se enquadra nos demais tipos.")]
    Outro = 11,
}
