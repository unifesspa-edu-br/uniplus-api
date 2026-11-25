namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Regras de campo da obrigatoriedade legal (ADR-0058, ADR-0125): cada método confere uma regra e devolve
/// as violações dela, com o nome do campo no formato do contrato; quem compõe junta todas, sem parar na primeira.
/// </summary>
internal static class ValidacaoDaObrigatoriedadeLegal
{
    internal const int TipoProcessoCodigoMaxLength = 64;
    internal const int RegraCodigoMaxLength = FormatoDoCodigo.ComprimentoMaximo;
    internal const int BaseLegalMaxLength = 500;
    internal const int DescricaoHumanaMaxLength = 1000;
    internal const int PortariaInternaCodigoMaxLength = 128;
    internal const int AtoNormativoMaxLength = 1000;

    internal static Result Validar(
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        string? regraCodigo,
        PredicadoObrigatoriedade? predicado,
        TextoNormativo texto,
        DateOnly? vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        ArgumentNullException.ThrowIfNull(texto);

        List<FieldError> erros = [];
        erros.AddRange(TipoProcessoCodigo(tipoProcessoCodigo));
        erros.AddRange(Categoria(categoria));
        erros.AddRange(RegraCodigo(regraCodigo));
        erros.AddRange(Predicado(predicado));
        erros.AddRange(Texto(texto));
        erros.AddRange(Vigencia(vigenciaInicio, vigenciaFim));

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    internal static string Aparar(string? valor) => valor?.Trim() ?? string.Empty;

    internal static string? AparaOuNulo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static IEnumerable<FieldError> TipoProcessoCodigo(string? valor) =>
        Obrigatorio(
            valor,
            TipoProcessoCodigoMaxLength,
            "tipoProcessoCodigo",
            "ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio",
            "TipoProcessoCodigo é obrigatório — use \"*\" para regras universais.",
            "ObrigatoriedadeLegal.TipoProcessoCodigoInvalido");

    private static IEnumerable<FieldError> RegraCodigo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            yield return Erro("regraCodigo", "ObrigatoriedadeLegal.RegraCodigoObrigatorio", "RegraCodigo é obrigatório.");
        }
        else if (valor.Length > RegraCodigoMaxLength || !FormatoDoCodigo.TemFormatoValido(valor))
        {
            yield return Erro("regraCodigo", "ObrigatoriedadeLegal.RegraCodigoInvalido",
                "O código da regra deve começar com letra maiúscula e conter até 80 letras maiúsculas, dígitos ou sublinhados isolados.");
        }
    }

    private static IEnumerable<FieldError> Categoria(CategoriaObrigatoriedade? categoria)
    {
        if (categoria is { } informada && Enum.IsDefined(informada))
        {
            yield break;
        }

        yield return Erro(
            "categoria",
            "ObrigatoriedadeLegal.CategoriaInvalida",
            "Categoria inválida — informe uma das categorias aceitas.");
    }

    private static IEnumerable<FieldError> Predicado(PredicadoObrigatoriedade? predicado)
    {
        if (predicado is null)
        {
            yield return Erro("predicado", "ObrigatoriedadeLegal.PredicadoObrigatorio", "Predicado é obrigatório.");
            yield break;
        }

        foreach (FieldError violacao in ObrigatoriedadeLegal.ValidarFormaDoPredicado(predicado).Errors)
        {
            yield return violacao;
        }
    }

    private static IEnumerable<FieldError> Texto(TextoNormativo texto) =>
        DescricaoHumana(texto.DescricaoHumana)
            .Concat(BaseLegal(texto.BaseLegal))
            .Concat(Opcional(
                texto.AtoNormativo,
                AtoNormativoMaxLength,
                "atoNormativoUrl",
                "ObrigatoriedadeLegal.AtoNormativoUrlInvalido"))
            .Concat(Opcional(
                texto.PortariaInternaCodigo,
                PortariaInternaCodigoMaxLength,
                "portariaInternaCodigo",
                "ObrigatoriedadeLegal.PortariaInternaCodigoInvalido"));

    private static IEnumerable<FieldError> DescricaoHumana(string? valor) =>
        Obrigatorio(
            valor,
            DescricaoHumanaMaxLength,
            "descricaoHumana",
            "ObrigatoriedadeLegal.DescricaoHumanaObrigatoria",
            "DescricaoHumana é obrigatória.",
            "ObrigatoriedadeLegal.DescricaoHumanaInvalida");

    private static IEnumerable<FieldError> BaseLegal(string? valor) =>
        Obrigatorio(
            valor,
            BaseLegalMaxLength,
            "baseLegal",
            "ObrigatoriedadeLegal.BaseLegalObrigatoria",
            "BaseLegal é obrigatória.",
            "ObrigatoriedadeLegal.BaseLegalInvalida");

    private static IEnumerable<FieldError> Vigencia(DateOnly? inicio, DateOnly? fim)
    {
        if (inicio is null)
        {
            yield return Erro(
                "vigenciaInicio",
                "ObrigatoriedadeLegal.VigenciaInicioObrigatorio",
                "VigenciaInicio é obrigatório.");
        }
        else if (fim is { } f && f <= inicio)
        {
            yield return Erro(
                "vigenciaFim",
                "ObrigatoriedadeLegal.VigenciaInvalida",
                "VigenciaFim deve ser estritamente posterior a VigenciaInicio.");
        }
    }

    private static IEnumerable<FieldError> Obrigatorio(
        string? valor,
        int tamanhoMaximo,
        string campo,
        string codigoObrigatorio,
        string mensagemObrigatorio,
        string codigoTamanho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            yield return Erro(campo, codigoObrigatorio, mensagemObrigatorio);
        }
        else if (valor.Trim().Length > tamanhoMaximo)
        {
            yield return Erro(campo, codigoTamanho, MensagemDeTamanho(campo, tamanhoMaximo));
        }
    }

    private static IEnumerable<FieldError> Opcional(string? valor, int tamanhoMaximo, string campo, string codigoTamanho)
    {
        if (!string.IsNullOrWhiteSpace(valor) && valor.Trim().Length > tamanhoMaximo)
        {
            yield return Erro(campo, codigoTamanho, MensagemDeTamanho(campo, tamanhoMaximo));
        }
    }

    private static string MensagemDeTamanho(string campo, int tamanhoMaximo) =>
        $"{char.ToUpperInvariant(campo[0])}{campo[1..]} deve ter no máximo {tamanhoMaximo} caracteres.";

    private static FieldError Erro(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError(codigo, mensagem));
}
