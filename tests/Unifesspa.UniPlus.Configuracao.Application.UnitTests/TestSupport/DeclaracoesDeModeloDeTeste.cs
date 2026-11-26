namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>As declarações que os testes do modelo de formulário usam: se serve à fase de inscrição e se é obrigatório.</summary>
internal static class DeclaracoesDeModeloDeTeste
{
    public static readonly DeclaracaoDoFormulario DaInscricao = DeclaracaoDoFormulario.DaInscricao;

    public static readonly DeclaracaoDoFormulario DeOutraFaseObrigatoria = new(ServeAFaseDeInscricao: false, Obrigatorio: true);

    public static readonly DeclaracaoDoModeloInput EntradaDeOutraFaseObrigatoria = new(ServeAFaseDeInscricao: false, Obrigatorio: true);
}
