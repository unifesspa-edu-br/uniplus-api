namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
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
    /// Confere a declaração do pedido: dizer se o formulário é obrigatório é obrigatório, e a falta é recusada com o código de
    /// campo obrigatório. Enquanto a recusa existir nada é gravado; as conferências que não dependem do campo seguem
    /// tratando o formulário como facultativo, o que só dispensa as regras que dependem de ele ser obrigatório.
    /// </summary>
    public static (FormularioDeclarado Declarado, List<FieldError> Recusas) Declarar(CodigoDoFormulario codigo, Guid? faseId, bool? obrigatorio) =>
        (new FormularioDeclarado(codigo, faseId, obrigatorio ?? false),
         obrigatorio is null
            ? [new FieldError("obrigatorio", new DomainError(CodigosDeValidacao.Obrigatorio, "Informe se o formulário é obrigatório."))]
            : []);

    /// <summary>
    /// O que a estrutura das páginas confere do formulário: serve à fase de inscrição quando a fase que o recebe coleta
    /// inscrição, e também quando ainda não tem fase ou a leitura do documento não pôde julgá-la.
    /// </summary>
    public DeclaracaoDoFormulario Estrutura(CronogramaDoProcesso cronograma)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        return new(FaseId is null || cronograma.FaseSemJulgar(FaseId) || cronograma.FaseColetaInscricao(FaseId), Obrigatorio);
    }
}
