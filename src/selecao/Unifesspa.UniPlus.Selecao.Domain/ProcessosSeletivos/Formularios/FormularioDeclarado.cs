namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// O que a configuração declara de um formulário do processo para as regras de alcance e de código: o
/// código que o identifica, a fase do cronograma a que está ligado e se é obrigatório. A fase é nula
/// enquanto o rascunho ainda não a escolheu (ADR-0171).
/// </summary>
public sealed record FormularioDeclarado(CodigoDoFormulario Codigo, Guid? FaseId, bool Obrigatorio)
{
    /// <summary>
    /// O que a estrutura das páginas confere do formulário: serve à fase de inscrição quando a fase que o recebe coleta
    /// inscrição, e também quando ainda não tem fase ou a leitura do documento não pôde julgá-la.
    /// </summary>
    public DeclaracaoDoFormulario Estrutura(CronogramaDoProcesso cronograma)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        return new(FaseId is null || cronograma.FaseSemJulgar(FaseId) || cronograma.FaseColetaInscricao(FaseId), Obrigatorio);
    }

    // Provisório até a retirada da finalidade: o código dos três formulários conhecidos dá a finalidade deles; os demais seguem a obrigatoriedade.
    public FinalidadeFormulario FinalidadeProvisoria => FinalidadeProvisoriaDe(Codigo.Valor, Obrigatorio);

    public static FinalidadeFormulario FinalidadeProvisoriaDe(string codigo, bool obrigatorio) =>
        EstruturaFormulario.FinalidadeDoCodigo(codigo) ?? CorrespondenciaDaFinalidade.FinalidadeDa(new(false, obrigatorio));

    // Provisório até a retirada da finalidade: as operações que ainda recebem a finalidade declaram o formulário pelo código dela.
    public static FormularioDeclarado DaFinalidade(FinalidadeFormulario finalidade, Guid? faseId) => new(
        CodigoDoFormulario.Criar(finalidade.Codigo()).Value!,
        faseId is { } fase && fase != Guid.Empty ? fase : null,
        CorrespondenciaDaFinalidade.DeclaracaoDa(finalidade).Obrigatorio);
}
