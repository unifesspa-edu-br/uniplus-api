namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

public static class FormulariosDeclaradosErrorCodes
{
    /// <summary>Dois formulários do processo têm o mesmo código.</summary>
    public const string CodigoDuplicado = "FormularioProcesso.CodigoDuplicado";

    /// <summary>A exigência de documento cita um formulário que o processo não tem.</summary>
    public const string ExigenciaEmFormularioInexistente = "FormularioProcesso.ExigenciaEmFormularioInexistente";

    /// <summary>A exigência de documento é entregue por formulário facultativo, que o candidato pode não enviar.</summary>
    public const string ExigenciaEmFormularioFacultativo = "FormularioProcesso.ExigenciaEmFormularioFacultativo";

    /// <summary>A exigência de documento é de uma fase, e o formulário que a entrega é de outra.</summary>
    public const string ExigenciaEmFormularioDeOutraFase = "FormularioProcesso.ExigenciaEmFormularioDeOutraFase";

    /// <summary>O formulário a remover ainda é o que entrega alguma exigência de documento.</summary>
    public const string RemocaoDeFormularioComExigencia = "FormularioProcesso.RemocaoDeFormularioComExigencia";
}
