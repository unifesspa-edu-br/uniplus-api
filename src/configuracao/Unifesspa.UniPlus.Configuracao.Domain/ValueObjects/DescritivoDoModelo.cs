namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O descritivo do modelo de formulário: o nome, a descrição e o tipo de processo a que se destina — nulo
/// quando serve a todos. As recusas dele apontam o campo dentro de <see cref="Caminho"/>, que é onde o
/// descritivo vai no corpo da escrita.
/// </summary>
public sealed record DescritivoDoModelo(string? Nome, string? Descricao, string? TipoProcessoCodigo)
{
    /// <summary>O caminho do descritivo no corpo da escrita, onde as recusas dele se apontam.</summary>
    public const string Caminho = "descritivo";

    private static readonly CampoDeCodigo CampoTipoProcesso = new(
        "Código do tipo de processo",
        ModeloFormulario.TipoProcessoCodigoMaxLength,
        ModeloFormularioErrorCodes.TipoProcessoCodigoEmBranco,
        ModeloFormularioErrorCodes.TipoProcessoCodigoFormatoInvalido);

    /// <summary>As recusas do nome, da descrição e do tipo de processo, todas juntas, no caminho do descritivo.</summary>
    public List<FieldError> Conferir() =>
    [
        .. NoDescritivo(
        [
            .. TextoGravavel.Recusar("nome", Nome),
            .. TextoGravavel.Recusar("descricao", Descricao),
            .. ValidarNome(),
            .. ValidarDescricao(),
            .. ValidarTipoProcesso(),
        ]),
    ];

    private static IEnumerable<FieldError> NoDescritivo(IEnumerable<FieldError> erros) =>
        erros.Select(static e => e.NoCaminhoDe(Caminho));

    private IEnumerable<FieldError> ValidarNome()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            yield return new("nome", new DomainError(ModeloFormularioErrorCodes.NomeObrigatorio, "O nome do modelo é obrigatório."));
        }
        else if (Nome.Trim().Length > ModeloFormulario.NomeMaxLength)
        {
            yield return new("nome", new DomainError(
                ModeloFormularioErrorCodes.NomeTamanho, $"O nome do modelo deve ter no máximo {ModeloFormulario.NomeMaxLength} caracteres."));
        }
    }

    private IEnumerable<FieldError> ValidarDescricao()
    {
        if (FormaDoItem.TextoOpcional(Descricao) is { Length: > ModeloFormulario.DescricaoMaxLength })
        {
            yield return new("descricao", new DomainError(
                ModeloFormularioErrorCodes.DescricaoTamanho,
                $"A descrição do modelo deve ter no máximo {ModeloFormulario.DescricaoMaxLength} caracteres."));
        }
    }

    private IEnumerable<FieldError> ValidarTipoProcesso()
    {
        if (TipoProcessoCodigo is not null && CampoTipoProcesso.Recusa(TipoProcessoCodigo, "tipoProcessoCodigo") is { } erro)
        {
            yield return erro;
        }
    }
}
