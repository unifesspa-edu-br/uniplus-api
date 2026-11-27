namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fada;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O Fator de Atenuação das Desigualdades de Acesso (FADA) do documento. O processo que não o aplica não pode trazer a configuração
/// dele, pela regra da declaração. O que o aplica e traz a configuração a tem lida pela fábrica da entidade, depois de a regra do
/// catálogo e a base legal existirem; os campos que faltam são recusados no campo de cada um. O bloco é um só: qualquer recusa o
/// deixa fora, sem declarar se aplica. O que aplica e não traz configuração alguma fica declarado, e quem o recusa é a conformidade.
/// </summary>
internal static class LeituraDoFada
{
    /// <summary>O nome do bloco, que é o caminho da parte recusada.</summary>
    internal const string Caminho = "fada";

    internal static FadaLido Ler(FadaNoDocumento? fada, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(cadastros);

        return fada switch
        {
            null => FadaLido.Ausente,
            { Aplica: false } => NaoAplicado(fada),
            _ when !TemConfiguracao(fada) => new(true, null, []),
            _ => Aplicado(fada, cadastros),
        };
    }

    private static FadaLido NaoAplicado(FadaNoDocumento fada) =>
        DeclaracaoDoFada.Recusa(aplica: false, TemConfiguracao(fada)) is { } recusa
            ? Recusado([new FieldError(null, recusa)])
            : new(false, null, []);

    private static FadaLido Aplicado(FadaNoDocumento fada, CadastrosCitadosNoDocumento cadastros)
    {
        RegraDoCatalogoCitada? regra = cadastros.Regra(fada.Regra?.Codigo, fada.Regra?.Versao);
        BaseLegalCongelada? baseLegal = cadastros.BaseLegalDoFada(fada.BaseLegalCodigo);
        FieldError[] recusas = [.. CamposObrigatorios(fada), .. RecusaDaRegra(fada, regra), .. RecusaDaBaseLegal(fada, baseLegal)];

        return recusas.Length > 0
            ? Recusado(recusas)
            : Criado(fada, regra!, baseLegal!);
    }

    private static FadaLido Criado(FadaNoDocumento fada, RegraDoCatalogoCitada regra, BaseLegalCongelada baseLegal)
    {
        Result<ConfiguracaoDoFada> criada = ConfiguracaoDoFada.Criar(
            regra.Referencia, fada.Fator!.Value, fada.Teto, fada.CodigoDoDadoDeElegibilidade, baseLegal);

        // A recusa que não aponta campo é a da base legal sem município, que é o que a base legal citada deixa de dar.
        return criada.IsSuccess
            ? new(true, criada.Value, [])
            : Recusado([.. criada.Errors.Select(static causa => causa with { Field = causa.Field ?? "baseLegalCodigo" })]);
    }

    private static bool TemConfiguracao(FadaNoDocumento fada) =>
        fada.Regra is not null || fada.Fator is not null || fada.Teto is not null
        || fada.CodigoDoDadoDeElegibilidade is not null || fada.BaseLegalCodigo is not null;

    private static IEnumerable<FieldError> CamposObrigatorios(FadaNoDocumento fada) =>
    [
        .. Obrigatorio(fada.Regra?.Codigo is null, "regra.codigo"),
        .. Obrigatorio(fada.Regra?.Versao is null, "regra.versao"),
        .. Obrigatorio(fada.Fator is null, "fator"),
        .. Obrigatorio(fada.BaseLegalCodigo is null, "baseLegalCodigo"),
    ];

    private static IEnumerable<FieldError> Obrigatorio(bool ausente, string campo) =>
        ausente ? [new FieldError(campo, RecusasDeCadastroDasVagas.CamposDoFadaObrigatorios())] : [];

    private static IEnumerable<FieldError> RecusaDaRegra(FadaNoDocumento fada, RegraDoCatalogoCitada? regra) =>
        fada.Regra is not { Codigo: not null, Versao: not null }
            ? []
            : regra switch
            {
                null => [new FieldError("regra.codigo", RecusasDeCadastroDasVagas.RegraDoFadaNaoEncontrada())],
                { Tipo: not TipoRegra.RegraFada } => [new FieldError("regra.codigo", RecusasDeCadastroDasVagas.RegraDoFadaDeTipoInvalido())],
                _ => [],
            };

    private static IEnumerable<FieldError> RecusaDaBaseLegal(FadaNoDocumento fada, BaseLegalCongelada? baseLegal) =>
        fada.BaseLegalCodigo is not null && baseLegal is null
            ? [new FieldError("baseLegalCodigo", RecusasDeCadastroDasVagas.BaseLegalNaoEncontrada())]
            : [];

    private static FadaLido Recusado(IEnumerable<FieldError> causas) => new(null, null, RecusasDasVagas.Partes(Caminho, 0, Caminho, causas));
}
