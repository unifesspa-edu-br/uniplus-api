namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>Monta a coleta de um formulário só com o que a conferência olha: o fato de cada campo e o que as regras citam.</summary>
internal static class ColetaDeTeste
{
    internal static FormularioDeclarado Obrigatorio(string codigo, FaseCronograma fase) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, fase.Id, Obrigatorio: true);

    internal static FormularioDeclarado Facultativo(string codigo, FaseCronograma fase) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, fase.Id, Obrigatorio: false);

    internal static ColetaDoFormulario Coleta(FormularioDeclarado formulario, params string[] fatos) =>
        new(formulario, [.. fatos.Select(static (fato, indice) => new ItemDoGrafo(fato, indice + 1, null, []))], [], [], []);

    internal static ColetaDoFormulario ColetaComGrupo(FormularioDeclarado formulario, string grupo, params string[] camposDoGrupo) =>
        new(
            formulario,
            [],
            [new GrupoDoGrafo(grupo, 1, null, [], [.. camposDoGrupo.Select(static (campo, indice) => new ItemDoGrafo(campo, indice + 1, null, []))])],
            [],
            []);
}
